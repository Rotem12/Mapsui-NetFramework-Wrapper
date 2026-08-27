using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Mapsui48.Client
{
    /// <summary>
    /// Floating tactical circular button at bottom-left of the map for opening map & marker styling.
    /// </summary>
    public class MapStyleButtonOverlay : Form
    {
        private MapHostPanel _parentPanel;
        private Rectangle _rectStyle = new Rectangle(0, 0, 36, 36);
        private bool _isHovered = false;
        private Timer _animationTimer;
        private float _scaleStyle = 1.0f;

        public event EventHandler StyleClicked;

        public MapStyleButtonOverlay(MapHostPanel parentPanel)
        {
            _parentPanel = parentPanel;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Black;
            this.Opacity = 0.75;
            this.Size = new Size(36, 36);
            this.Cursor = Cursors.Hand;

            var path = new GraphicsPath();
            path.AddEllipse(_rectStyle);
            this.Region = new Region(path);

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _animationTimer = new Timer { Interval = 16 };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        public void AttachEvents()
        {
            HookParentForm();
            _parentPanel.ParentChanged += (s, e) => HookParentForm();
            _parentPanel.Resize += (s, e) => UpdatePosition();
            _parentPanel.VisibleChanged += (s, e) => UpdatePosition();
            _parentPanel.HandleCreated += (s, e) => UpdatePosition();
            UpdatePosition();
        }

        private void HookParentForm()
        {
            var form = _parentPanel.FindForm();
            if (form != null)
            {
                form.LocationChanged -= Form_PositionOrVisibilityChanged;
                form.LocationChanged += Form_PositionOrVisibilityChanged;
                form.SizeChanged -= Form_PositionOrVisibilityChanged;
                form.SizeChanged += Form_PositionOrVisibilityChanged;
                form.Shown -= Form_PositionOrVisibilityChanged;
                form.Shown += Form_PositionOrVisibilityChanged;
                form.Activated -= Form_PositionOrVisibilityChanged;
                form.Activated += Form_PositionOrVisibilityChanged;
                form.VisibleChanged -= Form_PositionOrVisibilityChanged;
                form.VisibleChanged += Form_PositionOrVisibilityChanged;
            }
        }

        private void Form_PositionOrVisibilityChanged(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (_parentPanel.IsDisposed || !_parentPanel.Visible || !_parentPanel.IsHandleCreated)
            {
                this.Hide();
                return;
            }

            var form = _parentPanel.FindForm();
            if (form == null || !form.Visible)
            {
                this.Hide();
                return;
            }

            int y = _parentPanel.Height - 46;
            if (y < 10) y = 10;
            var screenPos = _parentPanel.PointToScreen(new Point(10, y));
            if (this.Location != screenPos)
            {
                this.Location = screenPos;
            }

            if (!this.Visible)
            {
                this.Show(form);
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            float target = _isHovered ? 1.25f : 1.0f;
            if (Math.Abs(_scaleStyle - target) < 0.01f)
            {
                _scaleStyle = target;
                _animationTimer.Stop();
            }
            else
            {
                _scaleStyle += (target - _scaleStyle) * 0.3f;
            }
            this.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            if (!_animationTimer.Enabled) _animationTimer.Start();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            if (!_animationTimer.Enabled) _animationTimer.Start();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                StyleClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw circular background
            using (var bgBrush = new SolidBrush(Color.Black))
            {
                g.FillEllipse(bgBrush, _rectStyle);
            }

            // Draw tactical styling icon (Palette symbol)
            float cx = _rectStyle.Width / 2f;
            float cy = _rectStyle.Height / 2f;
            float size = 18f * _scaleStyle;

            using (var pen = new Pen(Color.White, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                g.DrawEllipse(pen, cx - size / 2, cy - size / 2, size, size);

                using (var dotBrush = new SolidBrush(Color.White))
                {
                    float dotR = 1.6f * _scaleStyle;
                    g.FillEllipse(dotBrush, cx - size / 3.8f - dotR, cy - size / 3.8f - dotR, dotR * 2, dotR * 2);
                    g.FillEllipse(dotBrush, cx + size / 4.5f - dotR, cy - size / 3.8f - dotR, dotR * 2, dotR * 2);
                    g.FillEllipse(dotBrush, cx - size / 3.8f - dotR, cy + size / 5f - dotR, dotR * 2, dotR * 2);
                    g.FillEllipse(dotBrush, cx + size / 5f - dotR * 1.1f, cy + size / 5f - dotR * 1.1f, dotR * 2.2f, dotR * 2.2f);
                }
            }
        }
    }
}
