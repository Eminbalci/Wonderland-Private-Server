using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Wonderland_Private_Server
{
    /// <summary>
    /// Centralized UI design system, color palette, typography, and control styling engine.
    /// </summary>
    public static class ModernTheme
    {
        #region Color Palette

        // Slate Neutrals
        public static readonly Color Slate950 = Color.FromArgb(2, 6, 23);
        public static readonly Color Slate900 = Color.FromArgb(15, 23, 42);
        public static readonly Color Slate800 = Color.FromArgb(30, 41, 59);
        public static readonly Color Slate700 = Color.FromArgb(51, 65, 85);
        public static readonly Color Slate600 = Color.FromArgb(71, 85, 105);
        public static readonly Color Slate500 = Color.FromArgb(100, 116, 139);
        public static readonly Color Slate400 = Color.FromArgb(148, 163, 184);
        public static readonly Color Slate300 = Color.FromArgb(203, 213, 225);
        public static readonly Color Slate200 = Color.FromArgb(226, 232, 240);
        public static readonly Color Slate100 = Color.FromArgb(241, 245, 249);
        public static readonly Color Slate50 = Color.FromArgb(248, 250, 252);

        // Accent Colors
        public static readonly Color Blue600 = Color.FromArgb(37, 99, 235);
        public static readonly Color Blue700 = Color.FromArgb(29, 78, 216);
        public static readonly Color Blue200 = Color.FromArgb(191, 219, 254);
        public static readonly Color Blue100 = Color.FromArgb(219, 234, 254);
        public static readonly Color Blue50 = Color.FromArgb(239, 246, 255);

        public static readonly Color Emerald600 = Color.FromArgb(5, 150, 105);
        public static readonly Color Emerald700 = Color.FromArgb(4, 120, 87);
        public static readonly Color Emerald200 = Color.FromArgb(167, 243, 208);
        public static readonly Color Emerald100 = Color.FromArgb(209, 250, 229);
        public static readonly Color Emerald50 = Color.FromArgb(236, 253, 245);

        public static readonly Color Rose600 = Color.FromArgb(225, 29, 72);
        public static readonly Color Rose700 = Color.FromArgb(190, 18, 60);
        public static readonly Color Rose200 = Color.FromArgb(254, 205, 211);
        public static readonly Color Rose100 = Color.FromArgb(255, 228, 230);
        public static readonly Color Rose50 = Color.FromArgb(255, 241, 242);

        public static readonly Color Amber600 = Color.FromArgb(217, 119, 6);
        public static readonly Color Amber700 = Color.FromArgb(180, 83, 9);
        public static readonly Color Amber200 = Color.FromArgb(253, 230, 138);
        public static readonly Color Amber100 = Color.FromArgb(254, 243, 199);
        public static readonly Color Amber50 = Color.FromArgb(254, 243, 199);

        public static readonly Color Violet600 = Color.FromArgb(124, 58, 237);
        public static readonly Color Violet700 = Color.FromArgb(109, 40, 217);
        public static readonly Color Violet200 = Color.FromArgb(221, 214, 254);
        public static readonly Color Violet100 = Color.FromArgb(237, 233, 254);
        public static readonly Color Violet50 = Color.FromArgb(245, 243, 255);

        // Surfaces
        public static readonly Color PageBackground = Color.FromArgb(243, 245, 249);
        public static readonly Color CardBackground = Color.White;
        public static readonly Color BorderColor = Slate200;
        public static readonly Color BorderStrong = Slate300;

        #endregion

        #region Fonts

        public static readonly Font FontDefault = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontBoldSmall = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font FontBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontSemibold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
        public static readonly Font FontHeading = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font FontMonospace = new Font("Consolas", 9f, FontStyle.Regular);

        #endregion

        #region Helpers

        private class Tone
        {
            public Color C50, C100, C200, C600, C700;
            public Tone(Color c50, Color c100, Color c200, Color c600, Color c700)
            {
                C50 = c50; C100 = c100; C200 = c200; C600 = c600; C700 = c700;
            }
        }

        private static readonly Tone ToneBlue = new Tone(Blue50, Blue100, Blue200, Blue600, Blue700);
        private static readonly Tone ToneEmerald = new Tone(Emerald50, Emerald100, Emerald200, Emerald600, Emerald700);
        private static readonly Tone ToneRose = new Tone(Rose50, Rose100, Rose200, Rose600, Rose700);
        private static readonly Tone ToneAmber = new Tone(Color.FromArgb(255, 251, 235), Amber100, Amber200, Amber600, Amber700);
        private static readonly Tone ToneViolet = new Tone(Violet50, Violet100, Violet200, Violet600, Violet700);

        // Controls (and their children) the theme must leave alone, e.g. hand-built header bars.
        private static readonly ConditionalWeakTable<Control, object> excluded = new ConditionalWeakTable<Control, object>();
        // Controls that already have a custom paint hook attached.
        private static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

        public static void Exclude(Control c)
        {
            if (c == null) return;
            excluded.Remove(c);
            excluded.Add(c, null);
        }

        private static bool IsExcluded(Control c) => excluded.TryGetValue(c, out _);

        private static bool TryHook(Control c)
        {
            if (hooked.TryGetValue(c, out _)) return false;
            hooked.Add(c, null);
            return true;
        }

        private static bool IsSystemBack(Color c) =>
            c == SystemColors.Control || c == SystemColors.ButtonFace || c == SystemColors.ControlLight;

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width < d || r.Height < d)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Shows grey placeholder text in an empty TextBox.</summary>
        public static void SetPlaceholder(TextBox tb, string text)
        {
            if (tb == null) return;
            void apply() { try { SendMessage(tb.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, text); } catch { } }
            if (tb.IsHandleCreated) apply();
            else tb.HandleCreated += (s, e) => apply();
        }

        private static void DisableMnemonicForAmpersand(Control c)
        {
            // "A & B" is never meant as a keyboard shortcut; without this WinForms swallows the "&".
            string text = c.Text;
            if (string.IsNullOrEmpty(text) || text.IndexOf("& ", StringComparison.Ordinal) < 0) return;
            if (c is ButtonBase bb) bb.UseMnemonic = false;
            else if (c is Label l) l.UseMnemonic = false;
        }

        #endregion

        #region Double Buffering

        public static void EnableDoubleBuffering(Control c)
        {
            if (c == null) return;
            try
            {
                PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                pi?.SetValue(c, true, null);
            }
            catch { }
        }

        #endregion

        #region Universal Control Styling

        public static void Apply(Control root)
        {
            if (root == null || IsExcluded(root)) return;

            try
            {
                StyleControl(root);
            }
            catch { }

            foreach (Control child in root.Controls)
            {
                Apply(child);
            }
        }

        private static void StyleControl(Control c)
        {
            switch (c)
            {
                case Form f:
                    if (IsSystemBack(f.BackColor)) f.BackColor = PageBackground;
                    break;

                case DataGridView dgv:
                    StyleGrid(dgv);
                    break;

                case Button btn:
                    StyleButton(btn);
                    DisableMnemonicForAmpersand(btn);
                    break;

                case CheckBox _:
                case RadioButton _:
                    if (c.Font.Name != "Segoe UI") c.Font = new Font("Segoe UI", c.Font.Size, c.Font.Style);
                    if (IsSystemBack(c.BackColor) && c.Parent != null) c.BackColor = c.Parent.BackColor;
                    DisableMnemonicForAmpersand(c);
                    break;

                case Label lbl:
                    if (lbl.Font.Name != "Segoe UI" && lbl.Font.Name != "Consolas")
                    {
                        lbl.Font = new Font("Segoe UI", lbl.Font.Size, lbl.Font.Style);
                    }
                    if (IsSystemBack(lbl.BackColor) && lbl.Parent != null) lbl.BackColor = lbl.Parent.BackColor;
                    DisableMnemonicForAmpersand(lbl);
                    break;

                case GroupBox grp:
                    StyleCard(grp);
                    break;

                case TabPage tp:
                    if (IsSystemBack(tp.BackColor))
                    {
                        tp.UseVisualStyleBackColor = false;
                        tp.BackColor = PageBackground;
                    }
                    break;

                case TabControl tc:
                    EnableDoubleBuffering(tc);
                    if (tc.Font.Name != "Segoe UI") tc.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    StyleTabStrip(tc);
                    break;

                case RichTextBox rtb:
                    if (rtb.BorderStyle == BorderStyle.Fixed3D) rtb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case TextBox tb:
                    if (tb.Font.Name != "Segoe UI" && tb.Font.Name != "Consolas")
                    {
                        tb.Font = new Font("Segoe UI", tb.Font.Size < 9f ? 9f : tb.Font.Size, tb.Font.Style);
                    }
                    if (tb.BorderStyle == BorderStyle.Fixed3D) tb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListBox lb:
                    if (lb.Font.Name != "Segoe UI") lb.Font = new Font("Segoe UI", 9f, lb.Font.Style);
                    if (lb.BorderStyle == BorderStyle.Fixed3D) lb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case NumericUpDown nud:
                    if (nud.BorderStyle == BorderStyle.Fixed3D) nud.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case Panel _:
                case SplitContainer _:
                    if (IsSystemBack(c.BackColor) && c.Parent != null) c.BackColor = c.Parent.BackColor;
                    if (c is Panel p && p.BorderStyle == BorderStyle.Fixed3D) p.BorderStyle = BorderStyle.FixedSingle;
                    break;
            }
        }

        /// <summary>Turns a GroupBox into a white rounded card with its title inside.</summary>
        public static void StyleCard(GroupBox grp)
        {
            if (grp == null) return;

            grp.Font = new Font("Segoe UI", grp.Font.Size < 9f ? 9f : grp.Font.Size, FontStyle.Bold);
            grp.ForeColor = Slate700;
            if (IsSystemBack(grp.BackColor)) grp.BackColor = CardBackground;

            if (!TryHook(grp)) return;
            EnableDoubleBuffering(grp);
            grp.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.Clear(grp.Parent?.BackColor ?? PageBackground);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, grp.Width - 1, grp.Height - 1);
                using (GraphicsPath path = RoundedRect(rect, 6))
                using (var fill = new SolidBrush(grp.BackColor))
                using (var pen = new Pen(BorderColor))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }

                if (!string.IsNullOrEmpty(grp.Text))
                {
                    TextRenderer.DrawText(g, grp.Text, grp.Font, new Point(9, 2), grp.ForeColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                }
            };
        }

        /// <summary>Owner-draws a classic top tab strip as flat tabs with an accent underline.</summary>
        public static void StyleTabStrip(TabControl tc)
        {
            if (tc == null) return;
            // Leave hidden-header hosts and already custom-drawn controls untouched.
            if (tc.Appearance != TabAppearance.Normal || tc.Alignment != TabAlignment.Top || tc.DrawMode != TabDrawMode.Normal) return;
            if (!TryHook(tc)) return;

            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.Padding = new Point(12, 6);
            tc.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
                TabPage page = tc.TabPages[e.Index];
                bool selected = e.Index == tc.SelectedIndex;
                Rectangle r = e.Bounds;

                using (var bg = new SolidBrush(selected ? CardBackground : Slate100))
                {
                    e.Graphics.FillRectangle(bg, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4));
                }
                if (selected)
                {
                    using (var accent = new SolidBrush(Blue600))
                    {
                        e.Graphics.FillRectangle(accent, new Rectangle(r.X, r.Bottom - 2, r.Width, 2));
                    }
                }

                TextRenderer.DrawText(e.Graphics, page.Text, tc.Font, r, selected ? Blue700 : Slate600,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
        }

        public static void StyleGrid(DataGridView dgv)
        {
            if (dgv == null) return;

            try
            {
                EnableDoubleBuffering(dgv);

                dgv.BackgroundColor = Color.White;
                dgv.BorderStyle = BorderStyle.None;
                dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                dgv.GridColor = Slate100;
                dgv.RowHeadersVisible = false;
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dgv.MultiSelect = false;
                dgv.AllowUserToResizeRows = false;
                dgv.EnableHeadersVisualStyles = false;

                // Column Headers: light, quiet header row
                dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Slate100;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Slate600;
                dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Slate100;
                dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Slate600;
                dgv.ColumnHeadersDefaultCellStyle.Font = FontBoldSmall;
                dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
                dgv.ColumnHeadersHeight = 32;
                dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

                // Default Row Style
                dgv.DefaultCellStyle.BackColor = Color.White;
                dgv.DefaultCellStyle.ForeColor = Slate800;
                dgv.DefaultCellStyle.SelectionBackColor = Blue100;
                dgv.DefaultCellStyle.SelectionForeColor = Slate900;
                dgv.DefaultCellStyle.Font = FontDefault;
                dgv.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
                dgv.RowTemplate.Height = 28;

                // Alternating Rows
                dgv.AlternatingRowsDefaultCellStyle.BackColor = Slate50;
                dgv.AlternatingRowsDefaultCellStyle.ForeColor = Slate800;
                dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Blue100;
                dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Slate900;

                // Stretch columns when they would leave an empty strip on the right.
                if (TryHook(dgv))
                {
                    dgv.DataBindingComplete += (s, e) => FillColumnsIfNarrow(dgv);
                    dgv.SizeChanged += (s, e) => FillColumnsIfNarrow(dgv);
                }
            }
            catch { }
        }

        private static void FillColumnsIfNarrow(DataGridView dgv)
        {
            try
            {
                if (dgv.AutoSizeColumnsMode != DataGridViewAutoSizeColumnsMode.None || dgv.Columns.Count == 0) return;
                int used = 0;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (col.Visible) used += col.Width;
                }
                if (used > 0 && used < dgv.ClientSize.Width - 4)
                {
                    dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                }
            }
            catch { }
        }

        private static Tone ToneForHue(float hue)
        {
            if (hue < 18f || hue >= 330f) return ToneRose;
            if (hue < 70f) return ToneAmber;
            if (hue < 170f) return ToneEmerald;
            if (hue < 255f) return ToneBlue;
            return ToneViolet;
        }

        private static void ApplyNeutral(Button btn)
        {
            btn.BackColor = Color.White;
            if (btn.ForeColor == SystemColors.ControlText || btn.ForeColor == Color.Black) btn.ForeColor = Slate700;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = BorderStrong;
            btn.FlatAppearance.MouseOverBackColor = Slate50;
            btn.FlatAppearance.MouseDownBackColor = Slate100;
        }

        public static void StyleButton(Button btn, bool primary = false)
        {
            if (btn == null) return;

            try
            {
                Color bc = btn.BackColor;
                Color parentBack = btn.Parent?.BackColor ?? SystemColors.Control;

                if (primary)
                {
                    btn.BackColor = Blue600;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.FlatAppearance.MouseOverBackColor = Blue700;
                }
                else if (bc.A == 0)
                {
                    // Transparent buttons are custom chrome; keep their look.
                    btn.FlatAppearance.BorderSize = 0;
                }
                else if (IsSystemBack(bc) || bc == parentBack || bc == Color.White || bc == SystemColors.Window)
                {
                    ApplyNeutral(btn);
                }
                else if (bc.IsNamedColor)
                {
                    // Legacy designer colors (LightGreen, LightCoral, SteelBlue...) are mapped onto the palette.
                    float sat = bc.GetSaturation();
                    float bri = bc.GetBrightness();
                    if (sat < 0.12f && bri > 0.6f)
                    {
                        ApplyNeutral(btn);
                    }
                    else if (sat < 0.12f)
                    {
                        btn.BackColor = Slate700;
                        btn.ForeColor = Color.White;
                        btn.FlatAppearance.BorderSize = 0;
                        btn.FlatAppearance.MouseOverBackColor = Slate800;
                    }
                    else
                    {
                        Tone tone = ToneForHue(bc.GetHue());
                        if (bri > 0.70f)
                        {
                            btn.BackColor = tone.C50;
                            btn.ForeColor = tone.C700;
                            btn.FlatAppearance.BorderSize = 1;
                            btn.FlatAppearance.BorderColor = tone.C200;
                            btn.FlatAppearance.MouseOverBackColor = tone.C100;
                            btn.FlatAppearance.MouseDownBackColor = tone.C200;
                        }
                        else
                        {
                            btn.BackColor = tone.C600;
                            btn.ForeColor = Color.White;
                            btn.FlatAppearance.BorderSize = 0;
                            btn.FlatAppearance.MouseOverBackColor = tone.C700;
                            btn.FlatAppearance.MouseDownBackColor = tone.C700;
                        }
                    }
                }
                else
                {
                    // Hand-picked custom color: keep it, just flatten.
                    btn.FlatAppearance.BorderSize = 0;
                }

                btn.UseVisualStyleBackColor = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.Cursor = Cursors.Hand;
                if (btn.Font.Name != "Segoe UI")
                {
                    btn.Font = new Font("Segoe UI", btn.Font.Size < 9f ? 8.5f : btn.Font.Size, btn.Font.Style);
                }
            }
            catch { }
        }

        public static void StylePillButton(Button btn, bool active)
        {
            if (btn == null) return;

            try
            {
                if (active)
                {
                    btn.BackColor = Blue600;
                    btn.ForeColor = Color.White;
                    btn.Font = FontBoldSmall;
                }
                else
                {
                    btn.BackColor = Color.White;
                    btn.ForeColor = Slate700;
                    btn.Font = FontSmall;
                }
            }
            catch { }
        }

        public static void StyleCategoryButton(Button btn, bool active)
        {
            if (btn == null) return;

            try
            {
                if (active)
                {
                    btn.BackColor = Slate700;
                    btn.ForeColor = Color.White;
                }
                else
                {
                    btn.BackColor = Color.Transparent;
                    btn.ForeColor = Slate400;
                }
            }
            catch { }
        }

        #endregion
    }
}
