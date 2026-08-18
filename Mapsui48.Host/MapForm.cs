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
        }
        
        private bool _isAttached = false;
        
        private System.Windows.Forms.Timer? _mouseTimer;
        private Point _lastScreenPoint;
        private Point _dragStartClientPoint;
        private bool _isDraggingSelection = false;
        private bool _wasRightDown = false;
        private bool _wasShiftLeftDown = false;

        public void AttachMapControl()
        {
            _isAttached = true;
            if (!this.Controls.Contains(MapControl))
            {
                this.Controls.Add(MapControl);
            }
            
            // Start high-performance 40 FPS cursor tracking timer
            if (_mouseTimer == null)
            {
                _mouseTimer = new System.Windows.Forms.Timer { Interval = 25 };
                _mouseTimer.Tick += MouseTimer_Tick;
                _mouseTimer.Start();
            }

            // Now that we are reparented, make the form visible
            this.Visible = true;
        }

        private void MouseTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.Visible || MapControl == null || !MapControl.IsHandleCreated) return;

            Point screenPt = Control.MousePosition;
            Point clientPt = MapControl.PointToClient(screenPt);

            bool isInside = MapControl.ClientRectangle.Contains(clientPt);
            bool isRightDown = (Control.MouseButtons & MouseButtons.Right) != 0;
            bool isLeftDown = (Control.MouseButtons & MouseButtons.Left) != 0;
            bool isShiftDown = Control.ModifierKeys.HasFlag(Keys.Shift);
            bool isShiftLeftDown = isLeftDown && isShiftDown;

            // 1. Detect Mouse Down for Dragging / Selection
            if (isInside && !_isDraggingSelection)
            {
                if (isRightDown && !_wasRightDown)
                {
                    _dragStartClientPoint = clientPt;
                    _wasRightDown = true;
                }
                else if (isShiftLeftDown && !_wasShiftLeftDown)
                {
                    _dragStartClientPoint = clientPt;
                    _wasShiftLeftDown = true;
                }
            }

            // 2. Detect Dragging in Progress
            if (_wasRightDown || _wasShiftLeftDown)
            {
                int dx = Math.Abs(clientPt.X - _dragStartClientPoint.X);
                int dy = Math.Abs(clientPt.Y - _dragStartClientPoint.Y);
                if (dx > 8 || dy > 8)
                {
                    _isDraggingSelection = true;
                    UpdateSelectionPolygon(_dragStartClientPoint, clientPt);
                }
            }

            // 3. Detect Mouse Up / Release
            if (_wasRightDown && !isRightDown)
            {
                _wasRightDown = false;
                if (_isDraggingSelection)
                {
                    _isDraggingSelection = false;
                    FinishAreaSelection(_dragStartClientPoint, clientPt);
                }
                else if (isInside)
                {
                    // Standard Right Click
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
            }

            if (_wasShiftLeftDown && !isLeftDown)
            {
                _wasShiftLeftDown = false;
                if (_isDraggingSelection)
                {
                    _isDraggingSelection = false;
                    FinishAreaSelection(_dragStartClientPoint, clientPt);
                }
            }

            // 4. Pointer Movement (Hover)
            if (isInside && screenPt != _lastScreenPoint && !_isDraggingSelection)
            {
                _lastScreenPoint = screenPt;
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
