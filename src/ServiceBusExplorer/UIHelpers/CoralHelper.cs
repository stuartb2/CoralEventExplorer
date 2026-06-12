#region Using Directives

using FastColoredTextBoxNS;
using Newtonsoft.Json.Linq;
using ServiceBusExplorer.Utilities.Helpers;
using System;
using System.Drawing;
using System.Text;
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
            payloadGrouper.CustomPaint += e =>
                CopyBodyButtonHelper.LayoutTextBoxWithCopyButton(payloadGrouper, payloadTextBox, copyButton);

            parent.Controls.Remove(propertiesContainer);
            propertiesContainer.Dock = DockStyle.Fill;
            propertiesTabPage.Controls.Add(propertiesContainer);
            payloadTabPage.Controls.Add(payloadGrouper);
            tabControl.TabPages.Add(payloadTabPage);
            tabControl.TabPages.Add(propertiesTabPage);
            parent.Controls.Add(tabControl);

            bodyTextBox.TextChanged += (s, e) => SetPayloadText(bodyTextBox.Text, payloadTextBox);

            return payloadTextBox;
        }

        static void SetPayloadText(string messageBodyText, FastColoredTextBox payloadTextBox)
        {
            var payload = TryExtractPayload(messageBodyText);

            payloadTextBox.ClearStylesBuffer();
            payloadTextBox.Range.ClearStyle(StyleIndex.All);

            if (payload == null)
            {
                payloadTextBox.Language = Language.Custom;
                payloadTextBox.Text = string.Empty;
            }
            else if (JsonSerializerHelper.IsJson(payload))
            {
                payloadTextBox.Language = Language.JSON;
                payloadTextBox.Text = JsonSerializerHelper.Indent(payload);
            }
            else
            {
                payloadTextBox.Language = Language.Custom;
                payloadTextBox.Text = payload;
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
