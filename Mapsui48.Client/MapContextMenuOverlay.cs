using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mapsui48.Client
{
    /// <summary>
    /// Interactive tactical HUD context action panel displayed directly on the map upon right-click.
    /// Hosts customizable buttons with real-time looping icon animations and custom floating tooltips.
    /// </summary>
    public class MapContextMenuOverlay : Form, IMessageFilter
    {
        private readonly MapHostPanel _parentPanel;
        private readonly List<MapContextMenuItem> _items = new List<MapContextMenuItem>();
        private readonly MapCustomTooltipForm _tooltipForm;
        private readonly Timer _animTimer;
        private Form _hookedForm;
        private bool _filterInstalled;

        private float _animPhase = 0f;
        private int _hoverIndex = -1;
        private float[] _buttonScales = Array.Empty<float>();
        private RectangleF[] _buttonRects = Array.Empty<RectangleF>();

        public List<MapContextMenuItem> Items => _items;
        public DateTime ShowTime => _showTime;
        public double TargetLatitude { get; private set; }
        public double TargetLongitude { get; private set; }
        public Point TargetScreenPoint { get; private set; }

        public int ButtonSize { get; set; } = 42;
        public float IconSizeFactor { get; set; } = 1.0f;
        public int ButtonSpacing { get; set; } = 8;
        public int PaddingSize { get; set; } = 8;
        public bool ShowCoordinateHeader { get; set; } = true;
        public Color AccentColor { get; set; } = Color.White;

        private readonly Font _headerFont = new Font("Consolas", 8f, FontStyle.Bold);

        public MapContextMenuOverlay(MapHostPanel parentPanel)
        {
            _parentPanel = parentPanel;
            _tooltipForm = new MapCustomTooltipForm();

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.DimGray;
            this.ForeColor = Color.White;
            this.Cursor = Cursors.Hand;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _animTimer = new Timer { Interval = 16 }; // ~60 FPS
            _animTimer.Tick += AnimTimer_Tick;

            AttachParentEvents();
        }

        private DateTime _showTime = DateTime.MinValue;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private void AttachParentEvents()
        {
            _parentPanel.VisibleChanged += (s, e) => { if (!_parentPanel.Visible) HideMenu(true); };
            HookParentForm();
            _parentPanel.ParentChanged += (s, e) => HookParentForm();
        }

        private void HookParentForm()
        {
            var form = _parentPanel.FindForm();
            if (form != null && form != _hookedForm)
            {
                UnhookParentForm();
                _hookedForm = form;
                _hookedForm.Deactivate += ParentForm_Deactivate;
                _hookedForm.LocationChanged += ParentForm_Moved;
                _hookedForm.SizeChanged += ParentForm_Moved;
            }
        }

        private void UnhookParentForm()
        {
            if (_hookedForm != null)
            {
                _hookedForm.Deactivate -= ParentForm_Deactivate;
                _hookedForm.LocationChanged -= ParentForm_Moved;
                _hookedForm.SizeChanged -= ParentForm_Moved;
                _hookedForm = null;
            }
        }

        private void InstallMessageFilter()
        {
            if (!_filterInstalled)
            {
                Application.AddMessageFilter(this);
                _filterInstalled = true;
            }
        }

        private void RemoveMessageFilter()
        {
            if (_filterInstalled)
            {
                Application.RemoveMessageFilter(this);
                _filterInstalled = false;
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!this.Visible || this.IsDisposed) return false;

            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_RBUTTONDOWN = 0x0204;
            const int WM_MBUTTONDOWN = 0x0207;
            const int WM_NCLBUTTONDOWN = 0x00A1;
            const int WM_NCRBUTTONDOWN = 0x00A4;
            const int WM_NCMBUTTONDOWN = 0x00A7;
            const int WM_KEYDOWN = 0x0100;
            const int WM_ACTIVATEAPP = 0x001C;

            if (m.Msg == WM_KEYDOWN)
            {
                if ((Keys)(int)m.WParam == Keys.Escape)
                {
                    HideMenu(force: true);
                    return true;
                }
            }
            else if (m.Msg == WM_ACTIVATEAPP)
            {
                if (m.WParam == IntPtr.Zero)
                {
                    HideMenu(force: true);
                }
            }
            else if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN || m.Msg == WM_MBUTTONDOWN ||
                     m.Msg == WM_NCLBUTTONDOWN || m.Msg == WM_NCRBUTTONDOWN || m.Msg == WM_NCMBUTTONDOWN)
            {
                Point screenPt = Control.MousePosition;
                if (!this.Bounds.Contains(screenPt))
                {
                    HideMenu(force: true);
                }
            }

            return false;
        }

        private void ParentForm_Deactivate(object sender, EventArgs e)
        {
            HideMenu(true);
        }

        private void ParentForm_Moved(object sender, EventArgs e)
        {
            HideMenu(true);
        }

        public void AddItem(MapContextMenuItem item)
        {
            if (item != null)
            {
                _items.Add(item);
            }
        }

        public void AddItem(string name, string iconKey, Action<MapContextMenuContext> onClick, IconAnimationType animation = IconAnimationType.Pulse, string tooltip = null, Color? accentColor = null)
        {
            _items.Add(new MapContextMenuItem(name, iconKey, onClick, animation, tooltip, accentColor));
        }

        public void AddItem(string name, string iconKey, Func<MapContextMenuContext, Task> onClickAsync, IconAnimationType animation = IconAnimationType.Pulse, string tooltip = null, Color? accentColor = null)
        {
            _items.Add(new MapContextMenuItem(name, iconKey, onClickAsync, animation, tooltip, accentColor));
        }

        public void ClearItems()
        {
            _items.Clear();
            _hoverIndex = -1;
            _tooltipForm.HideTooltip();
        }

        public void ShowAt(Point screenPoint, double latitude, double longitude)
        {
            var visibleItems = _items.Where(i => i.IsVisible).ToList();
            if (visibleItems.Count == 0) return;

            HookParentForm();
            InstallMessageFilter();

            TargetLatitude = latitude;
            TargetLongitude = longitude;
            TargetScreenPoint = screenPoint;
            _hoverIndex = -1;
            _showTime = DateTime.UtcNow;

            _buttonScales = new float[visibleItems.Count];
            for (int i = 0; i < _buttonScales.Length; i++) _buttonScales[i] = 1.0f;
            _buttonRects = new RectangleF[visibleItems.Count];

            int headerHeight = ShowCoordinateHeader ? 22 : 0;
            int totalWidth = PaddingSize * 2 + visibleItems.Count * ButtonSize + (visibleItems.Count - 1) * ButtonSpacing;
            int totalHeight = PaddingSize * 2 + headerHeight + ButtonSize;

            totalWidth = Math.Max(totalWidth, ShowCoordinateHeader ? 140 : 60);

            // Calculate button layout rectangles
            float startX = PaddingSize + (totalWidth - (PaddingSize * 2 + visibleItems.Count * ButtonSize + (visibleItems.Count - 1) * ButtonSpacing)) / 2f;
            float btnY = PaddingSize + headerHeight;

            for (int i = 0; i < visibleItems.Count; i++)
            {
                _buttonRects[i] = new RectangleF(startX + i * (ButtonSize + ButtonSpacing), btnY, ButtonSize, ButtonSize);
            }

            this.Size = new Size(totalWidth, totalHeight);

            // Clamping position to remain within screen bounds
            var screen = Screen.FromPoint(screenPoint);
            var workArea = screen.WorkingArea;

            int x = screenPoint.X - totalWidth / 2;
            int y = screenPoint.Y - totalHeight - 12; // Anchor above clicked point by default

            if (y < workArea.Top + 10)
            {
                y = screenPoint.Y + 12;
            }

            if (x < workArea.Left + 8) x = workArea.Left + 8;
            if (x + totalWidth > workArea.Right - 8) x = workArea.Right - totalWidth - 8;
            if (y + totalHeight > workArea.Bottom - 8) y = workArea.Bottom - totalHeight - 8;

            this.Location = new Point(x, y);
            this.Region = CreateRoundedRegion(new Rectangle(0, 0, this.Width, this.Height), 8);

            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, x, y, totalWidth, totalHeight, SWP_SHOWWINDOW | 0x0010 /* SWP_NOACTIVATE */);
            this.BringToFront();
            this.Invalidate();

            _animPhase = 0f;
            if (!_animTimer.Enabled)
            {
                _animTimer.Start();
            }
        }

        public void HideMenu(bool force = true)
        {
            _hoverIndex = -1;
            _tooltipForm?.HideTooltip();
            _animTimer?.Stop();
            if (this.Visible)
            {
                this.Hide();
            }
            RemoveMessageFilter();
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                _animTimer.Stop();
                return;
            }

            _animPhase += 0.04f;
            if (_animPhase > 1000f) _animPhase = 0f;

            bool animatingScale = false;
            for (int i = 0; i < _buttonScales.Length; i++)
            {
                float target = (i == _hoverIndex) ? 1.15f : 1.0f;
                if (Math.Abs(_buttonScales[i] - target) > 0.01f)
                {
                    _buttonScales[i] += (target - _buttonScales[i]) * 0.35f;
                    animatingScale = true;
                }
                else
                {
                    _buttonScales[i] = target;
                }
            }

            // Always invalidate when hovering so 60fps looping icon animation renders continuously
            if (_hoverIndex >= 0 || animatingScale)
            {
                this.Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var visibleItems = _items.Where(i => i.IsVisible).ToList();

            int newHover = -1;
            for (int i = 0; i < _buttonRects.Length && i < visibleItems.Count; i++)
            {
                if (_buttonRects[i].Contains(e.Location) && visibleItems[i].IsEnabled)
                {
                    newHover = i;
                    break;
                }
            }

            if (_hoverIndex != newHover)
            {
                _hoverIndex = newHover;
                if (!_animTimer.Enabled) _animTimer.Start();

                if (_hoverIndex >= 0 && _hoverIndex < visibleItems.Count)
                {
                    var item = visibleItems[_hoverIndex];
                    Rectangle btnScreenRect = new Rectangle(
                        this.Location.X + (int)_buttonRects[_hoverIndex].X,
                        this.Location.Y + (int)_buttonRects[_hoverIndex].Y,
                        (int)_buttonRects[_hoverIndex].Width,
                        (int)_buttonRects[_hoverIndex].Height
                    );

                    string tooltipText = !string.IsNullOrEmpty(item.Tooltip) ? item.Tooltip : item.Name;
                    _tooltipForm.ShowTooltip(tooltipText, btnScreenRect, item.AccentColor ?? AccentColor);
                }
                else
                {
                    _tooltipForm.HideTooltip();
                }

                this.Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _tooltipForm.HideTooltip();
                this.Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            var visibleItems = _items.Where(i => i.IsVisible).ToList();
            if (_hoverIndex >= 0 && _hoverIndex < visibleItems.Count)
            {
                var item = visibleItems[_hoverIndex];
                if (!item.IsEnabled) return;

                var ctx = new MapContextMenuContext(
                    TargetLatitude,
                    TargetLongitude,
                    TargetScreenPoint,
                    _parentPanel.PointToClient(TargetScreenPoint),
                    _parentPanel,
                    item.Tag
                );

                HideMenu(force: true);

                if (item.OnClick != null)
                {
                    item.OnClick(ctx);
                }
                else if (item.OnClickAsync != null)
                {
                    _ = item.OnClickAsync(ctx);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            // 1. DimGray Tactical Panel Body
            using (var bgBrush = new SolidBrush(Color.FromArgb(245, 105, 105, 105)))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // 2. White Panel Border (similar to foreground)
            using (var borderPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.2f))
            {
                g.DrawRectangle(borderPen, rect);
            }

            // 3. Mini Coordinate Header / HUD Tag
            if (ShowCoordinateHeader)
            {
                string coordText = $"{TargetLatitude:F4}°, {TargetLongitude:F4}°";
                using (var headBrush = new SolidBrush(Color.White))
                using (var iconBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("🎯", _headerFont, iconBrush, new PointF(PaddingSize + 2, PaddingSize));
                    g.DrawString(coordText, _headerFont, headBrush, new PointF(PaddingSize + 18, PaddingSize + 1));
                }

                using (var linePen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
                {
                    float lineY = PaddingSize + 18f;
                    g.DrawLine(linePen, PaddingSize, lineY, this.Width - PaddingSize, lineY);
                }
            }

            // 4. Render Action Buttons & Animated Icons
            var visibleItems = _items.Where(i => i.IsVisible).ToList();
            for (int i = 0; i < visibleItems.Count && i < _buttonRects.Length; i++)
            {
                var item = visibleItems[i];
                var btnRect = _buttonRects[i];
                bool isHovered = (i == _hoverIndex);
                float scale = (i < _buttonScales.Length) ? _buttonScales[i] : 1.0f;
                Color itemAccent = item.AccentColor ?? Color.White;

                // Scaled button bounding box for animation
                float scaledW = btnRect.Width * scale;
                float scaledH = btnRect.Height * scale;
                float scaledX = btnRect.X + (btnRect.Width - scaledW) / 2f;
                float scaledY = btnRect.Y + (btnRect.Height - scaledH) / 2f;
                var animRect = new RectangleF(scaledX, scaledY, scaledW, scaledH);

                // Button Circle / Rounded Background
                using (var btnPath = new GraphicsPath())
                {
                    btnPath.AddEllipse(animRect);

                    if (isHovered)
                    {
                        using (var glowBrush = new PathGradientBrush(btnPath))
                        {
                            glowBrush.CenterPoint = new PointF(animRect.X + animRect.Width / 2f, animRect.Y + animRect.Height / 2f);
                            glowBrush.CenterColor = Color.FromArgb(220, 140, 140, 140);
                            glowBrush.SurroundColors = new[] { Color.FromArgb(240, 115, 115, 115) };
                            g.FillPath(glowBrush, btnPath);
                        }

                        using (var glowPen = new Pen(Color.White, 1.8f))
                        {
                            g.DrawPath(glowPen, btnPath);
                        }
                    }
                    else
                    {
                        using (var btnBrush = new SolidBrush(Color.FromArgb(180, 80, 80, 80)))
                        using (var btnPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
                        {
                            g.FillPath(btnBrush, btnPath);
                            g.DrawPath(btnPen, btnPath);
                        }
                    }
                }

                // Render Animated Looping Icon with IconSizeFactor * item.IconScale
                float iconScaleFactor = IconSizeFactor * item.IconScale;
                var iconRect = animRect;
                if (Math.Abs(iconScaleFactor - 1.0f) > 0.01f)
                {
                    float iconW = animRect.Width * iconScaleFactor;
                    float iconH = animRect.Height * iconScaleFactor;
                    float iconX = animRect.X + (animRect.Width - iconW) / 2f;
                    float iconY = animRect.Y + (animRect.Height - iconH) / 2f;
                    iconRect = new RectangleF(iconX, iconY, iconW, iconH);
                }

                MapIconAnimators.DrawAnimatedIcon(g, iconRect, item, _animPhase, isHovered);
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
                RemoveMessageFilter();
                UnhookParentForm();
                _animTimer?.Dispose();
                _tooltipForm?.Dispose();
                _headerFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
