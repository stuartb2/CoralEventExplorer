#region Using Directives

using Microsoft.ServiceBus.Messaging;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

#endregion

namespace ServiceBusExplorer.UIHelpers
{
    /// <summary>
    /// Coral: shows the raw body bytes of a message in a classic offset/hex/ASCII
    /// dump, reached from a "View Raw (Hex)" entry on the message list context menu.
    /// </summary>
    internal static class CoralHexView
    {
        const string MenuItemText = "View Raw (Hex)...";

        /// <summary>
        /// Appends a "View Raw (Hex)" item to a message list context menu. The supplied
        /// callback returns the message whose raw bytes should be shown (typically the
        /// currently selected/displayed message).
        /// </summary>
        internal static void AttachHexViewMenuItem(ContextMenuStrip menu, Func<BrokeredMessage> getMessage)
        {
            if (menu == null)
            {
                return;
            }
            menu.Items.Add(new ToolStripSeparator());
            var item = new ToolStripMenuItem(MenuItemText);
            item.Click += (s, e) => ShowHexView(getMessage());
            menu.Items.Add(item);
        }

        static void ShowHexView(BrokeredMessage message)
        {
            if (message == null)
            {
                MessageBox.Show("Select a message first.", "View Raw (Hex)",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var bytes = GetRawBody(message);
            if (bytes == null)
            {
                MessageBox.Show("The raw body of this message could not be read.", "View Raw (Hex)",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var title = $"Raw message (Hex) - {bytes.Length} bytes";
            if (!string.IsNullOrEmpty(SafeMessageId(message)))
            {
                title += $" - {SafeMessageId(message)}";
            }

            var form = new Form
            {
                Text = title,
                Width = 760,
                Height = 540,
                StartPosition = FormStartPosition.CenterScreen,
                ShowIcon = true
            };
            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 9.75F),
                Text = BuildHexDump(bytes)
            };
            form.Controls.Add(textBox);
            CoralTheme.Apply(form);
            textBox.Select(0, 0);
            form.Show();
        }

        static string SafeMessageId(BrokeredMessage message)
        {
            try
            {
                return message.MessageId;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Reads the raw body bytes from a clone of the message, so the message kept in
        // the grid is not consumed and can still be selected/resubmitted afterwards.
        static byte[] GetRawBody(BrokeredMessage message)
        {
            try
            {
                using (var clone = message.Clone())
                using (var stream = clone.GetBody<Stream>())
                {
                    if (stream == null)
                    {
                        return new byte[0];
                    }
                    if (stream.CanSeek)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string BuildHexDump(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "(empty body)";
            }
            var builder = new StringBuilder(bytes.Length * 4);
            for (var offset = 0; offset < bytes.Length; offset += 16)
            {
                builder.Append(offset.ToString("X8")).Append("  ");
                var ascii = new StringBuilder(16);
                for (var i = 0; i < 16; i++)
                {
                    if (offset + i < bytes.Length)
                    {
                        var b = bytes[offset + i];
                        builder.Append(b.ToString("X2")).Append(' ');
                        ascii.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                    }
                    else
                    {
                        builder.Append("   ");
                    }
                    if (i == 7)
                    {
                        builder.Append(' ');
                    }
                }
                builder.Append(' ').Append(ascii).Append(Environment.NewLine);
            }
            return builder.ToString();
        }
    }
}
