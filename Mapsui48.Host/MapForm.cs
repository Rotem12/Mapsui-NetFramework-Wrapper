using Mapsui48.Host.Services;
using Mapsui.UI.WindowsForms;
using Mapsui.Extensions;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace Mapsui48.Host
{
    public partial class MapForm : Form
    {
        public MapControl MapControl { get; private set; }
        private readonly MapService _mapService;
        private readonly CommandDispatcher _dispatcher;
        private readonly PipeServer _pipeServer;

        public MapForm(string pipeName)
        {
            InitializeComponent();

            // IMPORTANT: UseGPU is a static property in Mapsui 5.1.0 that dictates whether _glView or _canvasView is initialized.
            // It MUST be set before creating the MapControl instance, otherwise GetPixelDensity() throws NullReferenceException!
            MapControl.UseGPU = true;

            // Create the Mapsui MapControl
            MapControl = new MapControl
            {
                Dock = DockStyle.Fill
            };
            
            // We intentionally DO NOT add MapControl to this.Controls here.
            // We wait until AttachMapControl is called after IPC reparenting
            // to avoid WinForms CreateGraphics() NullReferenceExceptions when crossing process boundaries.

            // 1. Initialize MapService
            _mapService = new MapService(MapControl, SendEvent);

            // 2. Initialize Command Dispatcher
            _dispatcher = new CommandDispatcher(_mapService, this);

            // 3. Start Pipe Server
            _pipeServer = new PipeServer(pipeName, _dispatcher);
            _pipeServer.Start();

            // 4. Install Global Win32 Mouse Message Filter to catch right-clicks across all HWNDs (GLControl, Skia, etc.)
            Application.AddMessageFilter(new GlobalMouseMessageFilter(this));
        }
        
        private bool _isAttached = false;
        
        private Point _dragStartClientPoint;
        private bool _isDraggingSelection = false;
        private bool _wasShiftLeftDown = false;
        private bool _isRightMouseDown = false;
        private Point _rightClickStartPoint;
        private DateTime _lastPointerEventTime = DateTime.MinValue;

        public void AttachMapControl()
        {
            _isAttached = true;
            if (!this.Controls.Contains(MapControl))
            {
                this.Controls.Add(MapControl);
            }

            // Now that we are reparented, make the form visible
            this.Visible = true;
        }

        public void OnRawMouseDown(MouseButtons btn)
        {
            Point screenPt = Control.MousePosition;
            Point clientPt = MapControl.PointToClient(screenPt);

            if (!MapControl.ClientRectangle.Contains(clientPt)) return;

            if (btn == MouseButtons.Right)
            {
                MapService.LastRightClickTime = DateTime.UtcNow;
                _isRightMouseDown = true;
                _rightClickStartPoint = clientPt;
                _isDraggingSelection = false;
                try
                {
                    MapControl.Focus();
                }
                catch { }
            }
            else if (btn == MouseButtons.Left)
            {
                bool isShiftDown = Control.ModifierKeys.HasFlag(Keys.Shift);
                if (isShiftDown)
                {
                    _dragStartClientPoint = clientPt;
                    _wasShiftLeftDown = true;
                    _isDraggingSelection = false;
                }
            }
        }

        public void OnRawMouseMove()
        {
            Point screenPt = Control.MousePosition;
            Point clientPt = MapControl.PointToClient(screenPt);

            if (_isRightMouseDown)
            {
                int dx = Math.Abs(clientPt.X - _rightClickStartPoint.X);
                int dy = Math.Abs(clientPt.Y - _rightClickStartPoint.Y);
                // Area selection requires intentional drag > 15 pixels
                if (dx > 15 || dy > 15)
                {
                    _isDraggingSelection = true;
                    UpdateSelectionPolygon(_rightClickStartPoint, clientPt);
                }
            }
            else if (_wasShiftLeftDown)
            {
                int dx = Math.Abs(clientPt.X - _dragStartClientPoint.X);
                int dy = Math.Abs(clientPt.Y - _dragStartClientPoint.Y);
                if (dx > 15 || dy > 15)
                {
                    _isDraggingSelection = true;
                    UpdateSelectionPolygon(_dragStartClientPoint, clientPt);
                }
            }
            else
            {
                // Pointer Move (Hover) - throttled to ~30ms to prevent IPC pipe flooding
                var now = DateTime.UtcNow;
                if ((now - _lastPointerEventTime).TotalMilliseconds >= 30)
                {
                    _lastPointerEventTime = now;
                    var w = MapControl.Map.Navigator.Viewport.ScreenToWorld(clientPt.X, clientPt.Y);
                    var (lat, lon) = Helpers.CoordinateHelper.ToWgs84(w.X, w.Y);

                    SendEvent(new Protocol.MapPointerMovedEvent
                    {
                        Latitude = lat,
                        Longitude = lon,
                        ScreenX = clientPt.X,
                        ScreenY = clientPt.Y
                    });
                }
            }
        }

        private DateTime _lastRightClickEventSent = DateTime.MinValue;

        private void DispatchRightClick(Point clientPt)
        {
            if ((DateTime.UtcNow - _lastRightClickEventSent).TotalMilliseconds < 150) return;
            _lastRightClickEventSent = DateTime.UtcNow;
            MapService.LastRightClickTime = DateTime.UtcNow;

            var w = MapControl.Map.Navigator.Viewport.ScreenToWorld(clientPt.X, clientPt.Y);
            var (lat, lon) = Helpers.CoordinateHelper.ToWgs84(w.X, w.Y);
            SendEvent(new Protocol.MapClickedEvent
            {
                Latitude = lat,
                Longitude = lon,
                ScreenX = clientPt.X,
                ScreenY = clientPt.Y,
                Button = "Right"
            });
        }

        public void OnRawMouseUp(MouseButtons btn)
        {
            Point screenPt = Control.MousePosition;
            Point clientPt = MapControl.PointToClient(screenPt);

            if (btn == MouseButtons.Right)
            {
                bool wasDragging = _isDraggingSelection;
                _isRightMouseDown = false;
                _isDraggingSelection = false;

                if (wasDragging)
                {
                    FinishAreaSelection(_rightClickStartPoint, clientPt);
                }
                else
                {
                    DispatchRightClick(clientPt);
                }
            }
            else if (btn == MouseButtons.Left)
            {
                if (_wasShiftLeftDown)
                {
                    _wasShiftLeftDown = false;
                    if (_isDraggingSelection)
                    {
                        _isDraggingSelection = false;
                        FinishAreaSelection(_dragStartClientPoint, clientPt);
                    }
                }
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            // Prevent the form from being shown on the desktop before the client connects and reparents it.
            if (!_isAttached)
            {
                if (!IsHandleCreated) CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MapForm
            // 
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MapForm";
            this.Text = "Mapsui Host";
            this.ResumeLayout(false);
        }

        private void SendEvent(Protocol.MapEvent evt)
        {
            _pipeServer?.SendEvent(evt);
        }

        private void UpdateSelectionPolygon(Point p1, Point p2)
        {
            var w1 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p1.X, p1.Y);
            var w2 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p2.X, p2.Y);

            var (lat1, lon1) = Helpers.CoordinateHelper.ToWgs84(w1.X, w1.Y);
            var (lat2, lon2) = Helpers.CoordinateHelper.ToWgs84(w2.X, w2.Y);

            double minLat = Math.Min(lat1, lat2);
            double maxLat = Math.Max(lat1, lat2);
            double minLon = Math.Min(lon1, lon2);
            double maxLon = Math.Max(lon1, lon2);

            _mapService.AddPolygon(new Protocol.AddPolygonCommand
            {
                LayerName = "Selection",
                Coordinates = new Protocol.Coordinate[]
                {
                    new Protocol.Coordinate(maxLat, minLon),
                    new Protocol.Coordinate(maxLat, maxLon),
                    new Protocol.Coordinate(minLat, maxLon),
                    new Protocol.Coordinate(minLat, minLon),
                    new Protocol.Coordinate(maxLat, minLon)
                },
                FillColor = "#3300D4FF",
                OutlineColor = "#00D4FF",
                OutlineWidth = 2.0
            });
        }

        private void FinishAreaSelection(Point p1, Point p2)
        {
            var w1 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p1.X, p1.Y);
            var w2 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p2.X, p2.Y);

            var (lat1, lon1) = Helpers.CoordinateHelper.ToWgs84(w1.X, w1.Y);
            var (lat2, lon2) = Helpers.CoordinateHelper.ToWgs84(w2.X, w2.Y);

            double minLat = Math.Min(lat1, lat2);
            double maxLat = Math.Max(lat1, lat2);
            double minLon = Math.Min(lon1, lon2);
            double maxLon = Math.Max(lon1, lon2);

            SendEvent(new Protocol.AreaSelectedEvent
            {
                MinLat = minLat,
                MaxLat = maxLat,
                MinLon = minLon,
                MaxLon = maxLon
            });
        }

        public void OnRawMouseLeave()
        {
            SendEvent(new Protocol.MapPointerLeftEvent());
        }
    }

    internal class GlobalMouseMessageFilter : IMessageFilter
    {
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSELEAVE = 0x02A3;

        private const uint TME_LEAVE = 0x00000002;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct TRACKMOUSEEVENT
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        private readonly MapForm _form;
        private bool _isTrackingLeave = false;

        public GlobalMouseMessageFilter(MapForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_RBUTTONDOWN:
                    _form.OnRawMouseDown(MouseButtons.Right);
                    break;
                case WM_RBUTTONUP:
                    _form.OnRawMouseUp(MouseButtons.Right);
                    break;
                case WM_LBUTTONDOWN:
                    _form.OnRawMouseDown(MouseButtons.Left);
                    break;
                case WM_LBUTTONUP:
                    _form.OnRawMouseUp(MouseButtons.Left);
                    break;
                case WM_MOUSEMOVE:
                    if (!_isTrackingLeave)
                    {
                        _isTrackingLeave = true;
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<TRACKMOUSEEVENT>(),
                            dwFlags = TME_LEAVE,
                            hwndTrack = m.HWnd,
                            dwHoverTime = 0
                        };
                        TrackMouseEvent(ref tme);
                    }
                    _form.OnRawMouseMove();
                    break;
                case WM_MOUSELEAVE:
                    _isTrackingLeave = false;
                    _form.OnRawMouseLeave();
                    break;
            }
            return false;
        }
    }
}
