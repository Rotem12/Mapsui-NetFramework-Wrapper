using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Mapsui48.Client
{
    /// <summary>
    /// Lightweight non-activating custom tactical tooltip window that renders above all Win32 and OpenGL child surfaces.
    /// </summary>
    public class MapCustomTooltipForm : Form
    {
        private string _title = "";
        private string _description = "";
        private Color _accentColor = Color.FromArgb(0, 229, 255);

        private readonly Font _titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private readonly Font _descFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);

        public MapCustomTooltipForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.DimGray;
            ForeColor = Color.White;
            TopMost = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008 | 0x00000020; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_TRANSPARENT
                return cp;
            }
        }

        public void ShowTooltip(string text, Rectangle targetScreenBounds, Color? accent = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                HideTooltip();
                return;
            }

            _accentColor = accent ?? Color.FromArgb(0, 229, 255);

            string[] lines = text.Split(new[] { "\r\n", "\n" }, 2, StringSplitOptions.None);
            _title = lines[0];
            _description = lines.Length > 1 ? lines[1] : "";

            // Measure required dimensions
            using (var g = CreateGraphics())
            {
                var titleSize = g.MeasureString(_title, _titleFont);
                var descSize = !string.IsNullOrEmpty(_description) ? g.MeasureString(_description, _descFont) : SizeF.Empty;

                float width = Math.Max(titleSize.Width, descSize.Width) + 24f;
                float height = titleSize.Height + (descSize.Height > 0 ? descSize.Height + 6f : 0f) + 14f;

                width = Math.Max(width, 70f);
                height = Math.Max(height, 28f);

                this.Size = new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height));
            }

            // Calculate placement: Above target button, centered horizontally
            int cx = targetScreenBounds.Left + targetScreenBounds.Width / 2;
            int x = cx - this.Width / 2;
            int y = targetScreenBounds.Top - this.Height - 8;

            var screen = Screen.FromRectangle(targetScreenBounds);
            var workArea = screen.WorkingArea;

            // Flip below if too close to top of screen
            if (y < workArea.Top)
            {
                y = targetScreenBounds.Bottom + 8;
            }

            // Clamp X within screen
            if (x < workArea.Left + 4) x = workArea.Left + 4;
            if (x + this.Width > workArea.Right - 4) x = workArea.Right - this.Width - 4;

            this.Location = new Point(x, y);

            this.Region = CreateRoundedRegion(new Rectangle(0, 0, this.Width, this.Height), 6);
            this.Invalidate();

            if (!this.Visible)
            {
                this.Show();
            }
            this.BringToFront();
        }

        public void HideTooltip()
        {
            if (this.Visible)
            {
                this.Hide();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            // DimGray Tactical Background
            using (var brush = new SolidBrush(Color.FromArgb(245, 105, 105, 105)))
            {
                g.FillRectangle(brush, rect);
            }

            // Glowing Border
            using (var pen = new Pen(_accentColor, 1.2f))
            {
                g.DrawRectangle(pen, rect);
            }

            // Render Title Text
            float textY = 6f;
            using (var titleBrush = new SolidBrush(Color.White))
            {
                g.DrawString(_title, _titleFont, titleBrush, new PointF(10f, textY));
            }

            // Render Description Text (if present)
            if (!string.IsNullOrEmpty(_description))
            {
                var titleSize = g.MeasureString(_title, _titleFont);
                textY += titleSize.Height + 2f;
                using (var descBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                {
                    g.DrawString(_description, _descFont, descBrush, new PointF(10f, textY));
                }
            }
        }

        private static Region CreateRoundedRegion(Rectangle rect, int radius)
        {
            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return new Region(path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _descFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
