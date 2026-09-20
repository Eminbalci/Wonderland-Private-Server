using System;
using System.Drawing;
using System.Reflection;
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
        public static readonly Color Blue50 = Color.FromArgb(239, 246, 255);

        public static readonly Color Emerald600 = Color.FromArgb(5, 150, 105);
        public static readonly Color Emerald700 = Color.FromArgb(4, 120, 87);
        public static readonly Color Emerald50 = Color.FromArgb(236, 253, 245);

        public static readonly Color Rose600 = Color.FromArgb(225, 29, 72);
        public static readonly Color Rose700 = Color.FromArgb(190, 18, 60);
        public static readonly Color Rose50 = Color.FromArgb(255, 241, 242);

        public static readonly Color Amber600 = Color.FromArgb(217, 119, 6);
        public static readonly Color Amber700 = Color.FromArgb(180, 83, 9);
        public static readonly Color Amber50 = Color.FromArgb(254, 243, 199);

        #endregion

        #region Fonts

        public static readonly Font FontDefault = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontBoldSmall = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font FontBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontHeading = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font FontMonospace = new Font("Consolas", 9f, FontStyle.Regular);

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
            if (root == null) return;

            try
            {
                if (root is DataGridView dgv)
                {
                    StyleGrid(dgv);
                }
                else if (root is Button btn)
                {
                    StyleButton(btn);
                }
                else if (root is Label lbl)
                {
                    if (lbl.Font.Name != "Segoe UI" && lbl.Font.Name != "Consolas")
                    {
                        lbl.Font = new Font("Segoe UI", lbl.Font.Size, lbl.Font.Style);
                    }
                }
                else if (root is GroupBox grp)
                {
                    grp.Font = new Font("Segoe UI", grp.Font.Size < 9f ? 9f : grp.Font.Size, FontStyle.Bold);
                    grp.ForeColor = Slate800;
                }
                else if (root is TextBox tb)
                {
                    if (tb.Font.Name != "Segoe UI" && tb.Font.Name != "Consolas")
                    {
                        tb.Font = new Font("Segoe UI", tb.Font.Size < 9f ? 9f : tb.Font.Size, tb.Font.Style);
                    }
                }
                else if (root is ListBox lb)
                {
                    if (lb.Font.Name != "Segoe UI")
                    {
                        lb.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    }
                }
                else if (root is TabControl tc)
                {
                    EnableDoubleBuffering(tc);
                    if (tc.Font.Name != "Segoe UI")
                    {
                        tc.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    }
                }
            }
            catch { }

            foreach (Control child in root.Controls)
            {
                Apply(child);
            }
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
                dgv.GridColor = Slate200;
                dgv.RowHeadersVisible = false;
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dgv.MultiSelect = false;
                dgv.AllowUserToResizeRows = false;
                dgv.EnableHeadersVisualStyles = false;

                // Column Headers
                dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Slate800;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.Font = FontBold;
                dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
                dgv.ColumnHeadersHeight = 32;
                dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

                // Default Row Style
                dgv.DefaultCellStyle.BackColor = Color.White;
                dgv.DefaultCellStyle.ForeColor = Slate800;
                dgv.DefaultCellStyle.SelectionBackColor = Blue600;
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                dgv.DefaultCellStyle.Font = FontDefault;
                dgv.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
                dgv.RowTemplate.Height = 28;

                // Alternating Rows
                dgv.AlternatingRowsDefaultCellStyle.BackColor = Slate50;
                dgv.AlternatingRowsDefaultCellStyle.ForeColor = Slate800;
                dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Blue600;
                dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            }
            catch { }
        }

        public static void StyleButton(Button btn, bool primary = false)
        {
            if (btn == null) return;

            try
            {
                if (primary)
                {
                    btn.BackColor = Blue600;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.MouseOverBackColor = Blue700;
                }
                else if (btn.BackColor == SystemColors.Control || btn.BackColor == SystemColors.ControlLight)
                {
                    btn.BackColor = Slate100;
                    btn.ForeColor = Slate800;
                    btn.FlatAppearance.MouseOverBackColor = Slate200;
                }

                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
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
