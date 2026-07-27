using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Mapsui48.Client
{
    public class MapOverlayUI : Form
    {
        private MapHostPanel _parentPanel;
        
        // Use individual circles for each button
        private Rectangle _rectHome = new Rectangle(0, 0, 36, 36);
        private Rectangle _rectZoomIn = new Rectangle(0, 44, 36, 36);
        private Rectangle _rectZoomOut = new Rectangle(0, 88, 36, 36);
        
        private int _hoverIndex = -1; // 0=Home, 1=In, 2=Out
        private Timer _animationTimer;
        
        // Target scales for smooth animation
        private float _scaleHome = 1.0f;
        private float _scaleIn = 1.0f;
        private float _scaleOut = 1.0f;

        public MapOverlayUI(MapHostPanel parentPanel)
        {
            _parentPanel = parentPanel;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Black;
            this.Opacity = 0.75;
            this.Size = new Size(36, 124);
            this.Cursor = Cursors.Hand;
            
            // Set the region to make the form physically round for each button
            var path = new GraphicsPath();
            path.AddEllipse(_rectHome);
            path.AddEllipse(_rectZoomIn);
            path.AddEllipse(_rectZoomOut);
            this.Region = new Region(path);
            
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            
            _animationTimer = new Timer { Interval = 16 }; // ~60fps
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        public void AttachEvents()
        {
            var form = _parentPanel.FindForm();
            if (form != null)
            {
                form.LocationChanged += (s, e) => UpdatePosition();
                form.SizeChanged += (s, e) => UpdatePosition();
            }
            _parentPanel.Resize += (s, e) => UpdatePosition();
            _parentPanel.VisibleChanged += (s, e) => UpdatePosition();
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (_parentPanel.IsDisposed || !_parentPanel.Visible)
            {
                this.Hide();
                return;
            }
            
            var screenPos = _parentPanel.PointToScreen(new Point(10, 10)); // Top left with 10px margin
            if (this.Location != screenPos)
            {
                this.Location = screenPos;
            }
            
            if (!this.Visible && _parentPanel.Visible) 
            {
                this.Show(_parentPanel.FindForm());
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            bool changed = false;
            
            changed |= AnimateScale(ref _scaleHome, _hoverIndex == 0 ? 1.3f : 1.0f);
            changed |= AnimateScale(ref _scaleIn, _hoverIndex == 1 ? 1.3f : 1.0f);
            changed |= AnimateScale(ref _scaleOut, _hoverIndex == 2 ? 1.3f : 1.0f);
            
            if (changed) this.Invalidate();
        }
        
        private bool AnimateScale(ref float current, float target)
        {
            if (Math.Abs(current - target) < 0.01f)
            {
                current = target;
                return false;
            }
            
            current += (target - current) * 0.3f;
            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;
            if (_rectHome.Contains(e.Location)) newHover = 0;
            else if (_rectZoomIn.Contains(e.Location)) newHover = 1;
            else if (_rectZoomOut.Contains(e.Location)) newHover = 2;
            
            if (_hoverIndex != newHover)
            {
                _hoverIndex = newHover;
            }
        }
        
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_hoverIndex == 0) _parentPanel.GoHomeAsync(1500);
            else if (_hoverIndex == 1) _parentPanel.SetZoomAsync(_parentPanel.CurrentZoom + 1, 500);
            else if (_hoverIndex == 2) _parentPanel.SetZoomAsync(Math.Max(0, _parentPanel.CurrentZoom - 1), 500);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            DrawCrosshair(e.Graphics, _rectHome, _scaleHome);
            DrawPlus(e.Graphics, _rectZoomIn, _scaleIn);
            DrawMinus(e.Graphics, _rectZoomOut, _scaleOut);
        }
        
        private void DrawCrosshair(Graphics g, Rectangle r, float scale)
        {
            float size = 16 * scale;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            
            using (var pen = new Pen(Color.White, 2.5f))
            {
                g.DrawEllipse(pen, cx - size/2, cy - size/2, size, size);
                g.DrawLine(pen, cx, cy - size/2 - 4 * scale, cx, cy + size/2 + 4 * scale);
                g.DrawLine(pen, cx - size/2 - 4 * scale, cy, cx + size/2 + 4 * scale, cy);
            }
        }

        private void DrawPlus(Graphics g, Rectangle r, float scale)
        {
            float size = 14 * scale;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            
            using (var pen = new Pen(Color.White, 3f))
            {
                g.DrawLine(pen, cx, cy - size/2, cx, cy + size/2);
                g.DrawLine(pen, cx - size/2, cy, cx + size/2, cy);
            }
        }

        private void DrawMinus(Graphics g, Rectangle r, float scale)
        {
            float size = 14 * scale;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            
            using (var pen = new Pen(Color.White, 3f))
            {
                g.DrawLine(pen, cx - size/2, cy, cx + size/2, cy);
            }
        }
    }
}
