#region Using Directives

using System.Drawing;
using System.Windows.Forms;

#endregion

namespace ServiceBusExplorer.UIHelpers
{
    /// <summary>
    /// Previse/Coral brand palette (from 210210_PVS_ecosystem logo artwork) applied at
    /// runtime over the stock pale-blue theme, which is hard-coded throughout the
    /// designer files. Walks a control tree and swaps the stock colours for brand ones.
    /// </summary>
    internal static class CoralTheme
    {
        // Brand palette
        internal static readonly Color DeepTeal = Color.FromArgb(19, 68, 84);
        internal static readonly Color MidTeal = Color.FromArgb(65, 91, 107);
        internal static readonly Color Lime = Color.FromArgb(203, 212, 33);
        internal static readonly Color CoolGrey = Color.FromArgb(166, 174, 183);
        // Light teal-tinted surface keeping content areas readable
        internal static readonly Color Surface = Color.FromArgb(228, 236, 239);

        // Stock theme colours to replace
        static readonly Color StockPanelBlue = Color.FromArgb(215, 228, 242);
        static readonly Color StockBorderBlue = Color.FromArgb(153, 180, 209);

        internal static void Apply(Control root)
        {
            if (root == null)
            {
                return;
            }
            ApplyTo(root);
            foreach (Control child in root.Controls)
            {
                Apply(child);
            }
        }

        static bool Is(Color color, Color stock)
        {
            return color.ToArgb() == stock.ToArgb();
        }

        static void ApplyTo(Control control)
        {
            if (Is(control.BackColor, StockPanelBlue))
            {
                control.BackColor = Surface;
            }
            else if (Is(control.BackColor, StockBorderBlue))
            {
                control.BackColor = DeepTeal;
            }

            switch (control)
            {
                case Controls.Grouper grouper:
                    if (Is(grouper.BackgroundColor, StockPanelBlue))
                    {
                        grouper.BackgroundColor = Surface;
                    }
                    if (Is(grouper.BorderColor, StockBorderBlue))
                    {
                        grouper.BorderColor = MidTeal;
                    }
                    if (Is(grouper.CustomGroupBoxColor, StockBorderBlue))
                    {
                        grouper.CustomGroupBoxColor = DeepTeal;
                    }
                    break;

                case DataGridView grid:
                    if (Is(grid.BackgroundColor, StockBorderBlue))
                    {
                        grid.BackgroundColor = Surface;
                    }
                    if (Is(grid.GridColor, StockBorderBlue))
                    {
                        grid.GridColor = CoolGrey;
                    }
                    RestyleCell(grid.RowHeadersDefaultCellStyle);
                    RestyleCell(grid.ColumnHeadersDefaultCellStyle);
                    grid.DefaultCellStyle.SelectionBackColor = DeepTeal;
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    break;

                case Button button when button.FlatStyle == FlatStyle.Flat:
                    if (Is(button.FlatAppearance.BorderColor, StockBorderBlue))
                    {
                        button.FlatAppearance.BorderColor = MidTeal;
                    }
                    if (Is(button.FlatAppearance.MouseOverBackColor, StockBorderBlue))
                    {
                        button.FlatAppearance.MouseOverBackColor = Lime;
                    }
                    if (Is(button.FlatAppearance.MouseDownBackColor, StockBorderBlue))
                    {
                        button.FlatAppearance.MouseDownBackColor = Lime;
                    }
                    break;
            }
        }

        static void RestyleCell(DataGridViewCellStyle style)
        {
            if (Is(style.BackColor, StockPanelBlue))
            {
                style.BackColor = DeepTeal;
                style.ForeColor = Color.White;
            }
            if (Is(style.SelectionBackColor, StockPanelBlue) || Is(style.SelectionBackColor, StockBorderBlue))
            {
                style.SelectionBackColor = MidTeal;
                style.SelectionForeColor = Color.White;
            }
        }
    }
}
