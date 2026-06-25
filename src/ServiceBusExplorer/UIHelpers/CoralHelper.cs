#region Using Directives

using FastColoredTextBoxNS;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json.Linq;
using ServiceBusExplorer.Forms;
using ServiceBusExplorer.Helpers;
using ServiceBusExplorer.Utilities.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#endregion

namespace ServiceBusExplorer.UIHelpers
{
    /// <summary>
    /// Coral-specific customizations: defaulting message inspectors to the ZIP inspector
    /// and showing the decoded data_base64 payload of CloudEvents-style messages.
    /// </summary>
    internal static class CoralHelper
    {
        internal const string DefaultBrokeredMessageInspector = "ZipBrokeredMessageInspector";

        // Page size for the tail peek triggered by double-clicking a subscription and
        // by the "Older" paging button. Configurable via doubleClickPeekMessageCount.
        internal static readonly int PeekPageSize = GetPeekPageSize();

        static int GetPeekPageSize()
        {
            return int.TryParse(System.Configuration.ConfigurationManager.AppSettings["doubleClickPeekMessageCount"], out var count) && count > 0
                ? count
                : 50;
        }

        const string PayloadFieldName = "data_base64";
        const string PayloadPanelTitle = "Coral Payload (data_base64)";

        const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        static void SetCueBanner(TextBox textBox, string hint)
        {
            if (textBox.IsHandleCreated)
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, hint);
            }
            else
            {
                textBox.HandleCreated += (s, e) => SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, hint);
            }
        }

        /// <summary>
        /// Adds a full-width search strip above a message list grouper. The strip holds the
        /// search box plus an opt-in "Search payload" checkbox. The callback fires with the
        /// trimmed search text and the checkbox state as the user types (debounced) and
        /// immediately on Enter, when the box is cleared, or when the checkbox is toggled.
        /// The supplied <see cref="CancellationToken"/> is cancelled when a newer search
        /// supersedes the one in flight, so a long decode can be interrupted.
        /// </summary>
        internal static TextBox AddBodySearchBox(
            Controls.Grouper listGrouper, Func<string, bool, CancellationToken, Task> applySearch)
        {
            var searchBox = new TextBox
            {
                Name = "coralBodySearchBox_" + listGrouper.Name,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft Sans Serif", 8.25F)
            };
            SetCueBanner(searchBox, "Search message text...");

            // Opt-in, off by default: searching the decompressed data_base64 payload
            // means decoding every message body, so the user asks for it explicitly.
            var payloadCheckBox = new CheckBox
            {
                Name = "coralPayloadSearchCheckBox_" + listGrouper.Name,
                Text = "Search payload",
                AutoSize = true,
                Checked = false,
                Dock = DockStyle.Right,
                Padding = new Padding(6, 6, 2, 0),
                BackColor = Color.Transparent,
                Font = new Font("Microsoft Sans Serif", 8.25F)
            };
            new ToolTip().SetToolTip(payloadCheckBox,
                "Also search the decompressed data_base64 payload (slower).");

            // Only the most recent search may apply its results; cancelling the previous
            // token interrupts a decode that is no longer needed.
            CancellationTokenSource activeSearch = null;
            var lastText = (string)null;
            var lastIncludePayload = false;
            async void Apply()
            {
                var text = searchBox.Text.Trim();
                var includePayload = payloadCheckBox.Checked;
                if (text == lastText && includePayload == lastIncludePayload)
                {
                    return;
                }
                lastText = text;
                lastIncludePayload = includePayload;

                activeSearch?.Cancel();
                activeSearch = new CancellationTokenSource();
                var token = activeSearch.Token;
                try
                {
                    await applySearch(text, includePayload, token);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer search; nothing to apply.
                }
            }
            // Search incrementally as the user types, debounced so a fast typist
            // does not trigger a match computation per keystroke. Enter applies
            // immediately.
            var debounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                Apply();
            };
            searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    debounceTimer.Stop();
                    Apply();
                }
            };
            searchBox.TextChanged += (s, e) =>
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            };
            // Toggling the scope re-runs the current search immediately.
            payloadCheckBox.CheckedChanged += (s, e) =>
            {
                debounceTimer.Stop();
                Apply();
            };
            searchBox.Disposed += (s, e) =>
            {
                debounceTimer.Dispose();
                activeSearch?.Cancel();
            };

            var searchPanel = new Panel
            {
                Name = "coralBodySearchPanel_" + listGrouper.Name,
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };
            // Add the Fill control first, then the Right-docked checkbox, so the checkbox
            // reserves its edge and the search box fills the remainder.
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(payloadCheckBox);
            var parent = listGrouper.Parent;
            parent.Controls.Add(searchPanel);
            listGrouper.BringToFront();
            return searchBox;
        }

        // Decoding a message body (clone, read stream, gunzip) and extracting its
        // data_base64 payload (parse JSON, base64-decode) is too slow to repeat for every
        // message on every keystroke, so each is computed once per message and kept for the
        // message's lifetime. The payload is decoded lazily: when "Search payload" is off
        // (the default) it is never computed.
        sealed class CachedSearchText
        {
            public string Body;
            public bool PayloadDecoded;
            public string Payload; // decoded data_base64 payload, or null if absent
        }

        static readonly ConditionalWeakTable<BrokeredMessage, CachedSearchText> searchTextCache =
            new ConditionalWeakTable<BrokeredMessage, CachedSearchText>();

        static string BuildBodyText(ServiceBusHelper serviceBusHelper, BrokeredMessage message)
        {
            try
            {
                return serviceBusHelper.GetMessageText(message, MainForm.SingletonMainForm.UseAscii, out _)
                    ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns true when the message body text contains the search text
        /// (case-insensitive). When <paramref name="includePayload"/> is set, the decoded
        /// data_base64 payload is searched as well.
        /// </summary>
        internal static bool MessageMatches(
            ServiceBusHelper serviceBusHelper, BrokeredMessage message, string searchText, bool includePayload)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }
            var entry = searchTextCache.GetValue(
                message, m => new CachedSearchText { Body = BuildBodyText(serviceBusHelper, m) });
            if (entry.Body.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            if (!includePayload)
            {
                return false;
            }
            if (!entry.PayloadDecoded)
            {
                // A parallel search may decode the same message twice; the result is
                // identical, so the race is harmless and needs no lock.
                entry.Payload = TryExtractPayload(entry.Body);
                entry.PayloadDecoded = true;
            }
            return entry.Payload != null
                && entry.Payload.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Computes the set of messages matching the search text on a background thread,
        /// so the UI stays responsive while bodies are decoded. When
        /// <paramref name="includePayload"/> is set, the decoded data_base64 payload is
        /// searched too. The work observes <paramref name="cancellationToken"/> and throws
        /// <see cref="OperationCanceledException"/> if a newer search supersedes it. Returns
        /// null when the search text is empty (meaning: no filtering).
        /// </summary>
        internal static Task<HashSet<BrokeredMessage>> ComputeMatchesAsync(
            ServiceBusHelper serviceBusHelper, List<BrokeredMessage> messages, string searchText,
            bool includePayload, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return Task.FromResult<HashSet<BrokeredMessage>>(null);
            }
            return Task.Run(() => new HashSet<BrokeredMessage>(
                messages.AsParallel()
                    .WithCancellation(cancellationToken)
                    .Where(m => MessageMatches(serviceBusHelper, m, searchText, includePayload))),
                cancellationToken);
        }

        /// <summary>
        /// Selects the ZIP inspector in an inspector combo box, if available.
        /// The user can still pick a different inspector manually.
        /// </summary>
        internal static void SelectDefaultInspector(ComboBox inspectorComboBox)
        {
            var index = inspectorComboBox.Items.IndexOf(DefaultBrokeredMessageInspector);
            if (index >= 0)
            {
                inspectorComboBox.SelectedIndex = index;
            }
        }

        /// <summary>
        /// Wraps the right-hand properties pane in a tab control with a "Coral Payload" tab
        /// (selected by default) showing the decoded data_base64 payload, and a "Properties"
        /// tab holding the original system/custom property grids. The payload stays in sync
        /// with the body text box: whenever the body changes (row selection, refresh, clear),
        /// the payload is re-extracted from it.
        /// </summary>
        internal static FastColoredTextBox AttachPayloadTab(Control propertiesContainer, FastColoredTextBox bodyTextBox)
        {
            var parent = propertiesContainer.Parent;
            var backColor = Color.FromArgb(215, 228, 242);

            var tabControl = new TabControl
            {
                Name = "coralTabControl_" + bodyTextBox.Name,
                Dock = DockStyle.Fill
            };
            var payloadTabPage = new TabPage
            {
                Name = "coralPayloadTabPage_" + bodyTextBox.Name,
                Text = "Coral Payload",
                BackColor = backColor,
                Padding = new Padding(3)
            };
            var propertiesTabPage = new TabPage
            {
                Name = "coralPropertiesTabPage_" + bodyTextBox.Name,
                Text = "Properties",
                BackColor = backColor,
                Padding = new Padding(3)
            };

            var payloadGrouper = new Controls.Grouper
            {
                Name = "coralPayloadGrouper_" + bodyTextBox.Name,
                BackgroundColor = Color.FromArgb(215, 228, 242),
                BackgroundGradientColor = Color.White,
                BackgroundGradientMode = Controls.Grouper.GroupBoxGradientMode.None,
                BorderColor = Color.FromArgb(153, 180, 209),
                BorderThickness = 1F,
                CustomGroupBoxColor = Color.FromArgb(153, 180, 209),
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft Sans Serif", 8.25F),
                ForeColor = Color.White,
                GroupTitle = PayloadPanelTitle,
                Padding = new Padding(20),
                PaintGroupBox = true,
                RoundCorners = 4,
                ShadowColor = Color.DarkGray,
                ShadowControl = false,
                ShadowThickness = 1
            };

            var payloadTextBox = new FastColoredTextBox
            {
                Name = "coralPayloadTextBox_" + bodyTextBox.Name,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Courier New", 9.75F),
                ForeColor = SystemColors.ControlText,
                Location = new Point(16, 32),
                Language = Language.JSON,
                ShowFoldingLines = true
            };

            payloadGrouper.Controls.Add(payloadTextBox);
            var copyButton = CopyBodyButtonHelper.AddCopyBodyButton(payloadGrouper, payloadTextBox);

            var findBox = new TextBox
            {
                Name = "coralPayloadFindBox_" + bodyTextBox.Name,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft Sans Serif", 8.25F)
            };
            SetCueBanner(findBox, "Find in payload (Enter = next)");
            var highlightStyle = new TextStyle(Brushes.Black, Brushes.Gold, FontStyle.Regular);
            void HighlightMatches()
            {
                payloadTextBox.Range.ClearStyle(highlightStyle);
                var term = findBox.Text;
                if (!string.IsNullOrEmpty(term))
                {
                    foreach (var range in payloadTextBox.Range.GetRanges(Regex.Escape(term), RegexOptions.IgnoreCase))
                    {
                        range.SetStyle(highlightStyle);
                    }
                }
                payloadTextBox.Invalidate();
            }
            void GoToNextMatch()
            {
                var term = findBox.Text;
                if (string.IsNullOrEmpty(term))
                {
                    return;
                }
                var matches = payloadTextBox.Range.GetRanges(Regex.Escape(term), RegexOptions.IgnoreCase).ToList();
                if (matches.Count == 0)
                {
                    return;
                }
                var current = payloadTextBox.Selection.Start;
                var next = matches.FirstOrDefault(m =>
                    m.Start.iLine > current.iLine ||
                    (m.Start.iLine == current.iLine && m.Start.iChar > current.iChar)) ?? matches[0];
                payloadTextBox.Selection = next;
                payloadTextBox.DoSelectionVisible();
            }
            findBox.TextChanged += (s, e) => HighlightMatches();
            findBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    GoToNextMatch();
                }
            };
            payloadTextBox.TextChanged += (s, e) => HighlightMatches();

            var findPanel = new Panel
            {
                Name = "coralPayloadFindPanel_" + bodyTextBox.Name,
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 0, 0, 6),
                BackColor = backColor
            };
            findPanel.Controls.Add(findBox);

            payloadGrouper.CustomPaint += e =>
                CopyBodyButtonHelper.LayoutTextBoxWithCopyButton(payloadGrouper, payloadTextBox, copyButton);

            parent.Controls.Remove(propertiesContainer);
            propertiesContainer.Dock = DockStyle.Fill;
            propertiesTabPage.Controls.Add(propertiesContainer);
            payloadTabPage.Controls.Add(payloadGrouper);
            payloadTabPage.Controls.Add(findPanel);
            payloadGrouper.BringToFront();
            tabControl.TabPages.Add(payloadTabPage);
            tabControl.TabPages.Add(propertiesTabPage);
            parent.Controls.Add(tabControl);

            // Default the payload pane to half the detail view width from the start
            // (the split then scales proportionally with the window).
            if (parent.Parent is SplitContainer detailSplitContainer)
            {
                try
                {
                    detailSplitContainer.SplitterDistance =
                        (detailSplitContainer.Width - detailSplitContainer.SplitterWidth) / 2;
                }
                catch (InvalidOperationException)
                {
                    // Panel minimum sizes can reject the value at design-time sizes;
                    // the load-time assignment will apply it again.
                }
            }

            // Decoding and rendering the payload for every row is too slow when the user
            // scrolls through the message list, so the update is debounced: the panel
            // clears immediately, and the payload of the row the user settles on is
            // decoded on a background thread and displayed shortly after.
            var pendingBody = string.Empty;
            var payloadDebounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
            payloadDebounceTimer.Tick += async (s, e) =>
            {
                payloadDebounceTimer.Stop();
                var body = pendingBody;
                string payloadText;
                bool isJson;
                try
                {
                    (payloadText, isJson) = await Task.Run(() => ComputePayload(body));
                }
                catch (Exception)
                {
                    payloadText = string.Empty;
                    isJson = false;
                }
                if (!ReferenceEquals(body, pendingBody))
                {
                    return; // the user moved to another row while decoding
                }
                ApplyPayload(payloadTextBox, payloadText, isJson);
            };
            bodyTextBox.TextChanged += (s, e) =>
            {
                pendingBody = bodyTextBox.Text;
                if (payloadTextBox.TextLength > 0)
                {
                    ApplyPayload(payloadTextBox, string.Empty, false);
                }
                payloadDebounceTimer.Stop();
                payloadDebounceTimer.Start();
            };
            payloadTextBox.Disposed += (s, e) => payloadDebounceTimer.Dispose();

            return payloadTextBox;
        }

        static (string text, bool isJson) ComputePayload(string messageBodyText)
        {
            var payload = TryExtractPayload(messageBodyText);
            if (payload == null)
            {
                return (string.Empty, false);
            }
            if (JsonSerializerHelper.IsJson(payload))
            {
                return (JsonSerializerHelper.Indent(payload), true);
            }
            return (payload, false);
        }

        static void ApplyPayload(FastColoredTextBox payloadTextBox, string payloadText, bool isJson)
        {
            payloadTextBox.ClearStylesBuffer();
            payloadTextBox.Range.ClearStyle(StyleIndex.All);
            payloadTextBox.Language = isJson ? Language.JSON : Language.Custom;
            payloadTextBox.Text = payloadText;
            if (isJson)
            {
                // Make objects and arrays foldable via the +/- gutter markers.
                payloadTextBox.Range.SetFoldingMarkers("{", "}");
                payloadTextBox.Range.SetFoldingMarkers(@"\[", @"\]");
            }
        }

        static string TryExtractPayload(string messageBodyText)
        {
            if (string.IsNullOrWhiteSpace(messageBodyText))
            {
                return null;
            }
            try
            {
                var token = JObject.Parse(messageBodyText)[PayloadFieldName];
                if (token == null || token.Type != JTokenType.String)
                {
                    return null;
                }
                var bytes = Convert.FromBase64String((string)token);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
