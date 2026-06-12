#region Using Directives

using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

#endregion

namespace ServiceBusExplorer.UIHelpers
{
    /// <summary>
    /// Coral brand palette (from the Coral web app's variables.css) applied at
    /// runtime over the stock pale-blue theme, which is hard-coded throughout the
    /// designer files. Walks a control tree and swaps the stock colours for brand ones.
    /// </summary>
    internal static class CoralTheme
    {
        // Coral web app palette
        internal static readonly Color DeepTeal = Color.FromArgb(0x1e, 0x44, 0x55);   // --primary-color
        internal static readonly Color MidTeal = Color.FromArgb(0x3a, 0x51, 0x5c);    // --odd-row-color
        internal static readonly Color Lime = Color.FromArgb(0xe8, 0xe8, 0x70);       // --secondary-color
        internal static readonly Color Amber = Color.FromArgb(0xed, 0xad, 0x27);      // --secondary-dark-color
        internal static readonly Color CoolGrey = Color.FromArgb(0xb0, 0xb0, 0xb9);   // --grey-color
        // Light tint of the primary teal keeping content areas readable
        internal static readonly Color Surface = Color.FromArgb(232, 238, 241);

        // Stock theme colours to replace
        static readonly Color StockPanelBlue = Color.FromArgb(215, 228, 242);
        static readonly Color StockBorderBlue = Color.FromArgb(153, 180, 209);

        static bool rendererInstalled;

        static Icon appIcon;
        static bool appIconLoaded;

        static Icon AppIcon
        {
            get
            {
                if (!appIconLoaded)
                {
                    appIconLoaded = true;
                    try
                    {
                        appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch (System.Exception)
                    {
                        appIcon = null;
                    }
                }
                return appIcon;
            }
        }

        internal static void Apply(Control root)
        {
            if (root == null)
            {
                return;
            }
            if (!rendererInstalled)
            {
                ToolStripManager.Renderer = new CoralToolStripRenderer();
                rendererInstalled = true;
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
                case Form form:
                    form.BackColor = Surface;
                    // Forms embed their own (blue) copy of the logo in their resources;
                    // use the recoloured application icon instead.
                    if (AppIcon != null)
                    {
                        form.Icon = AppIcon;
                    }
                    break;

                case SplitContainer splitContainer:
                    if (Is(splitContainer.BackColor, SystemColors.Control))
                    {
                        splitContainer.BackColor = Surface;
                    }
                    break;

                case Controls.HeaderPanel headerPanel:
                    headerPanel.HeaderColor1 = MidTeal;
                    headerPanel.HeaderColor2 = DeepTeal;
                    headerPanel.ForeColor = Color.White;
                    break;

                case ToolStrip strip: // includes MenuStrip and StatusStrip
                    strip.BackColor = DeepTeal;
                    strip.ForeColor = Color.White;
                    // Yellow reads well against the dark teal chrome
                    RecolourImageList(strip.ImageList, YellowHue, 1f);
                    foreach (ToolStripItem item in strip.Items)
                    {
                        RestyleToolStripItem(item);
                    }
                    break;

                case TreeView treeView:
                    // Deep teal: dark enough to stay clear on the white tree background
                    RecolourImageList(treeView.ImageList, DeepTeal.GetHue(), 0.45f);
                    break;

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
                        button.FlatAppearance.MouseOverBackColor = Amber;
                    }
                    if (Is(button.FlatAppearance.MouseDownBackColor, StockBorderBlue))
                    {
                        button.FlatAppearance.MouseDownBackColor = Amber;
                    }
                    break;
            }
        }

        static void RestyleToolStripItem(ToolStripItem item)
        {
            item.ForeColor = Color.White;
            if (item.Image != null)
            {
                item.Image = RecolourBlues(item.Image, YellowHue, 1f);
            }
            if (item is ToolStripDropDownItem dropDownItem)
            {
                foreach (ToolStripItem child in dropDownItem.DropDownItems)
                {
                    RestyleToolStripItem(child);
                }
            }
        }

        const float YellowHue = 60f;

        static readonly HashSet<ImageList> recolouredImageLists = new HashSet<ImageList>();

        static void RecolourImageList(ImageList imageList, float targetHue, float valueScale)
        {
            if (imageList == null || !recolouredImageLists.Add(imageList))
            {
                return;
            }
            for (var i = 0; i < imageList.Images.Count; i++)
            {
                imageList.Images[i] = RecolourBlues(imageList.Images[i], targetHue, valueScale);
            }
        }

        /// <summary>
        /// Remaps blue hues in an image to the target hue, preserving each pixel's
        /// saturation and (scaled) brightness so shading survives.
        /// </summary>
        static Bitmap RecolourBlues(Image image, float targetHue, float valueScale)
        {
            var bitmap = new Bitmap(image);
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var colour = bitmap.GetPixel(x, y);
                    if (colour.A == 0)
                    {
                        continue;
                    }
                    RgbToHsv(colour, out var hue, out var saturation, out var value);
                    if (saturation > 0.15f && hue >= 160f && hue <= 280f)
                    {
                        bitmap.SetPixel(x, y, HsvToRgb(targetHue, saturation, System.Math.Min(1f, value * valueScale), colour.A));
                    }
                }
            }
            return bitmap;
        }

        static void RgbToHsv(Color colour, out float hue, out float saturation, out float value)
        {
            float r = colour.R / 255f, g = colour.G / 255f, b = colour.B / 255f;
            var max = System.Math.Max(r, System.Math.Max(g, b));
            var min = System.Math.Min(r, System.Math.Min(g, b));
            value = max;
            saturation = max == 0 ? 0 : (max - min) / max;
            hue = colour.GetHue();
        }

        static Color HsvToRgb(float hue, float saturation, float value, int alpha)
        {
            var hi = (int)(hue / 60f) % 6;
            var f = hue / 60f - (int)(hue / 60f);
            var v = value * 255f;
            var p = v * (1 - saturation);
            var q = v * (1 - f * saturation);
            var t = v * (1 - (1 - f) * saturation);
            switch (hi)
            {
                case 0: return Color.FromArgb(alpha, (int)v, (int)t, (int)p);
                case 1: return Color.FromArgb(alpha, (int)q, (int)v, (int)p);
                case 2: return Color.FromArgb(alpha, (int)p, (int)v, (int)t);
                case 3: return Color.FromArgb(alpha, (int)p, (int)q, (int)v);
                case 4: return Color.FromArgb(alpha, (int)t, (int)p, (int)v);
                default: return Color.FromArgb(alpha, (int)v, (int)p, (int)q);
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

        /// <summary>
        /// Renders all menus, toolbars, status bars and context menus (via the global
        /// ToolStripManager renderer) with white text on the teal chrome.
        /// </summary>
        class CoralToolStripRenderer : ToolStripProfessionalRenderer
        {
            public CoralToolStripRenderer() : base(new CoralColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item.Enabled)
                {
                    e.TextColor = Color.White;
                }
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.White;
                base.OnRenderArrow(e);
            }
        }

        /// <summary>
        /// Colour table for menus, toolbars and the status bar: deep teal chrome with
        /// mid-teal hover/selection so the white item text stays readable.
        /// </summary>
        class CoralColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => DeepTeal;
            public override Color MenuStripGradientEnd => DeepTeal;
            public override Color ToolStripGradientBegin => DeepTeal;
            public override Color ToolStripGradientMiddle => DeepTeal;
            public override Color ToolStripGradientEnd => DeepTeal;
            public override Color StatusStripGradientBegin => DeepTeal;
            public override Color StatusStripGradientEnd => DeepTeal;
            public override Color ToolStripDropDownBackground => DeepTeal;
            public override Color ImageMarginGradientBegin => DeepTeal;
            public override Color ImageMarginGradientMiddle => DeepTeal;
            public override Color ImageMarginGradientEnd => DeepTeal;
            public override Color MenuItemSelected => MidTeal;
            public override Color MenuItemSelectedGradientBegin => MidTeal;
            public override Color MenuItemSelectedGradientEnd => MidTeal;
            public override Color MenuItemPressedGradientBegin => MidTeal;
            public override Color MenuItemPressedGradientMiddle => MidTeal;
            public override Color MenuItemPressedGradientEnd => MidTeal;
            public override Color MenuItemBorder => Lime;
            public override Color MenuBorder => MidTeal;
            public override Color ButtonSelectedHighlight => MidTeal;
            public override Color ButtonSelectedGradientBegin => MidTeal;
            public override Color ButtonSelectedGradientMiddle => MidTeal;
            public override Color ButtonSelectedGradientEnd => MidTeal;
            public override Color ButtonSelectedBorder => Lime;
            public override Color ButtonPressedHighlight => MidTeal;
            public override Color ButtonPressedGradientBegin => MidTeal;
            public override Color ButtonPressedGradientMiddle => MidTeal;
            public override Color ButtonPressedGradientEnd => MidTeal;
            public override Color ButtonCheckedHighlight => MidTeal;
            public override Color ButtonCheckedGradientBegin => MidTeal;
            public override Color ButtonCheckedGradientMiddle => MidTeal;
            public override Color ButtonCheckedGradientEnd => MidTeal;
            public override Color SeparatorDark => MidTeal;
            public override Color SeparatorLight => MidTeal;
            public override Color GripDark => MidTeal;
            public override Color GripLight => DeepTeal;
            public override Color OverflowButtonGradientBegin => DeepTeal;
            public override Color OverflowButtonGradientMiddle => DeepTeal;
            public override Color OverflowButtonGradientEnd => DeepTeal;
            public override Color ToolStripBorder => DeepTeal;
            public override Color CheckBackground => MidTeal;
            public override Color CheckSelectedBackground => MidTeal;
            public override Color CheckPressedBackground => MidTeal;
        }
    }
}
