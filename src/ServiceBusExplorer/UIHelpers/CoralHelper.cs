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
        /// Adds a search box to a message list grouper header. The callback fires with the
        /// trimmed search text when the user presses Enter, and with an empty string when
        /// the box is cleared.
        /// </summary>
        internal static TextBox AddBodySearchBox(Controls.Grouper listGrouper, Action<string> applySearch)
        {
            var searchBox = new TextBox
            {
                Name = "coralBodySearchBox_" + listGrouper.Name,
                Location = new Point(152, 2),
                Size = new Size(200, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Font = new Font("Microsoft Sans Serif", 8.25F)
            };
            SetCueBanner(searchBox, "Search message text...");
            var lastApplied = string.Empty;
            void Apply()
            {
                var text = searchBox.Text.Trim();
                if (text == lastApplied)
                {
                    return;
                }
                lastApplied = text;
                applySearch(text);
            }
            // Search incrementally as the user types, debounced so a fast typist
            // does not trigger a match computation per keystroke. Enter applies
            // immediately.
            var debounceTimer = new Timer { Interval = 300 };
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
            searchBox.Disposed += (s, e) => debounceTimer.Dispose();
            listGrouper.Controls.Add(searchBox);
            searchBox.BringToFront();
            return searchBox;
        }

        // Decoding a message body (clone, read stream, gunzip, base64-decode the payload)
        // is too slow to repeat for every message on every keystroke, so the searchable
        // text is computed once per message and kept for the message's lifetime.
        static readonly ConditionalWeakTable<BrokeredMessage, string> searchTextCache =
            new ConditionalWeakTable<BrokeredMessage, string>();

        static string BuildSearchText(ServiceBusHelper serviceBusHelper, BrokeredMessage message)
        {
            string body;
            try
            {
                body = serviceBusHelper.GetMessageText(message, MainForm.SingletonMainForm.UseAscii, out _);
            }
            catch (Exception)
            {
                return string.Empty;
            }
            if (body == null)
            {
                return string.Empty;
            }
            var payload = TryExtractPayload(body);
            return payload == null ? body : body + "\n" + payload;
        }

        /// <summary>
        /// Returns true when the message body text, or its decoded data_base64 payload,
        /// contains the search text (case-insensitive).
        /// </summary>
        internal static bool MessageMatches(ServiceBusHelper serviceBusHelper, BrokeredMessage message, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }
            var text = searchTextCache.GetValue(message, m => BuildSearchText(serviceBusHelper, m));
            return text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Computes the set of messages matching the search text on a background thread,
        /// so the UI stays responsive while bodies are decoded. Returns null when the
        /// search text is empty (meaning: no filtering).
        /// </summary>
        internal static Task<HashSet<BrokeredMessage>> ComputeMatchesAsync(
            ServiceBusHelper serviceBusHelper, List<BrokeredMessage> messages, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return Task.FromResult<HashSet<BrokeredMessage>>(null);
            }
            return Task.Run(() => new HashSet<BrokeredMessage>(
                messages.AsParallel().Where(m => MessageMatches(serviceBusHelper, m, searchText))));
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
                Language = Language.JSON
            };

            payloadGrouper.Controls.Add(payloadTextBox);
            var copyButton = CopyBodyButtonHelper.AddCopyBodyButton(payloadGrouper, payloadTextBox);

            var findBox = new TextBox
            {
                Name = "coralPayloadFindBox_" + bodyTextBox.Name,
                Size = new Size(168, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
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
            payloadGrouper.Controls.Add(findBox);
            findBox.BringToFront();

            payloadGrouper.CustomPaint += e =>
            {
                CopyBodyButtonHelper.LayoutTextBoxWithCopyButton(payloadGrouper, payloadTextBox, copyButton);
                findBox.Location = new Point(copyButton.Location.X - findBox.Width - 8, 6);
            };

            parent.Controls.Remove(propertiesContainer);
            propertiesContainer.Dock = DockStyle.Fill;
            propertiesTabPage.Controls.Add(propertiesContainer);
            payloadTabPage.Controls.Add(payloadGrouper);
            tabControl.TabPages.Add(payloadTabPage);
            tabControl.TabPages.Add(propertiesTabPage);
            parent.Controls.Add(tabControl);

            // Decoding and rendering the payload for every row is too slow when the user
            // scrolls through the message list, so the update is debounced: the panel
            // clears immediately, and the payload of the row the user settles on is
            // decoded on a background thread and displayed shortly after.
            var pendingBody = string.Empty;
            var payloadDebounceTimer = new Timer { Interval = 200 };
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
