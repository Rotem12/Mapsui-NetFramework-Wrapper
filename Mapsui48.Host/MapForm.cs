using Mapsui48.Host.Services;
using Mapsui.UI.WindowsForms;
using Mapsui.Extensions;
using System;
using System.Windows.Forms;

using System.Drawing;

namespace Mapsui48.Host
{
    public partial class MapForm : Form, IMessageFilter
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
        }
        
        private bool _isAttached = false;
        
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

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;

        private bool _isRightDragging;
        private bool _isShiftLeftDragging;
        private Point _dragStartPoint;
        private long _lastHoverTick = 0;

        // Automatically add message filter so we can catch hover and selection
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Application.AddMessageFilter(this);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_RBUTTONDOWN)
            {
                var screenPoint = Control.MousePosition;
                var clientPoint = MapControl.PointToClient(screenPoint);
                if (MapControl.ClientRectangle.Contains(clientPoint))
                {
                    _dragStartPoint = clientPoint;
                    _isRightDragging = true;
                    return false;
                }
            }
            else if (m.Msg == WM_LBUTTONDOWN && Control.ModifierKeys.HasFlag(Keys.Shift))
            {
                var screenPoint = Control.MousePosition;
                var clientPoint = MapControl.PointToClient(screenPoint);
                if (MapControl.ClientRectangle.Contains(clientPoint))
                {
                    _dragStartPoint = clientPoint;
                    _isShiftLeftDragging = true;
                    return true; // Consume event to prevent default map panning
                }
            }
            else if (m.Msg == WM_MOUSEMOVE)
            {
                var screenPoint = Control.MousePosition;
                var clientPoint = MapControl.PointToClient(screenPoint);
                if (MapControl.ClientRectangle.Contains(clientPoint))
                {
                    if (_isRightDragging || _isShiftLeftDragging)
                    {
                        int dx = Math.Abs(clientPoint.X - _dragStartPoint.X);
                        int dy = Math.Abs(clientPoint.Y - _dragStartPoint.Y);
                        if (dx > 6 || dy > 6)
                        {
                            UpdateSelectionPolygon(_dragStartPoint, clientPoint);
                            return true;
                        }
                    }
                    else
                    {
                        // Throttled mouse hover event for floating tooltip (every 40ms)
                        long now = Environment.TickCount64;
                        if (now - _lastHoverTick >= 40)
                        {
                            _lastHoverTick = now;
                            var w = MapControl.Map.Navigator.Viewport.ScreenToWorld(clientPoint.X, clientPoint.Y);
                            var (lat, lon) = Helpers.CoordinateHelper.ToWgs84(w.X, w.Y);

                            SendEvent(new Protocol.MapPointerMovedEvent
                            {
                                Latitude = lat,
                                Longitude = lon,
                                ScreenX = clientPoint.X,
                                ScreenY = clientPoint.Y
                            });
                        }
                    }
                }
            }
            else if (m.Msg == WM_RBUTTONUP)
            {
                if (_isRightDragging)
                {
                    _isRightDragging = false;
                    var screenPoint = Control.MousePosition;
                    var clientPoint = MapControl.PointToClient(screenPoint);
                    int dx = Math.Abs(clientPoint.X - _dragStartPoint.X);
                    int dy = Math.Abs(clientPoint.Y - _dragStartPoint.Y);

                    if (dx > 6 || dy > 6)
                    {
                        // Finish Area Selection
                        FinishAreaSelection(_dragStartPoint, clientPoint);
                        return true;
                    }
                    else
                    {
                        // Standard Right-Click
                        var w = MapControl.Map.Navigator.Viewport.ScreenToWorld(clientPoint.X, clientPoint.Y);
                        var (lat, lon) = Helpers.CoordinateHelper.ToWgs84(w.X, w.Y);

                        SendEvent(new Protocol.MapClickedEvent
                        {
                            Latitude = lat,
                            Longitude = lon,
                            ScreenX = clientPoint.X,
                            ScreenY = clientPoint.Y,
                            Button = "Right"
                        });
                    }
                }
            }
            else if (m.Msg == WM_LBUTTONUP && _isShiftLeftDragging)
            {
                _isShiftLeftDragging = false;
                var screenPoint = Control.MousePosition;
                var clientPoint = MapControl.PointToClient(screenPoint);
                int dx = Math.Abs(clientPoint.X - _dragStartPoint.X);
                int dy = Math.Abs(clientPoint.Y - _dragStartPoint.Y);

                if (dx > 6 || dy > 6)
                {
                    FinishAreaSelection(_dragStartPoint, clientPoint);
                    return true;
                }
            }

            return false;
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
                FeatureId = "ActiveSelectionBox",
                Coordinates = new[]
                {
                    new Protocol.Coordinate(minLat, minLon),
                    new Protocol.Coordinate(minLat, maxLon),
                    new Protocol.Coordinate(maxLat, maxLon),
                    new Protocol.Coordinate(maxLat, minLon),
                    new Protocol.Coordinate(minLat, minLon)
                },
                FillColor = "#30FF0000",
                OutlineColor = "#FFFF0000",
                OutlineWidth = 2
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

            int minX = Math.Min(p1.X, p2.X);
            int minY = Math.Min(p1.Y, p2.Y);
            int width = Math.Abs(p1.X - p2.X);
            int height = Math.Abs(p1.Y - p2.Y);

            SendEvent(new Protocol.AreaSelectedEvent
            {
                MinLat = minLat,
                MinLon = minLon,
                MaxLat = maxLat,
                MaxLon = maxLon,
                ScreenX = minX,
                ScreenY = minY,
                ScreenWidth = width,
                ScreenHeight = height
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pipeServer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
