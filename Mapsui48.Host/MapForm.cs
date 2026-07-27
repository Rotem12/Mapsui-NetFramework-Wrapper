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
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;

        private bool _isSelectingArea;
        private bool _isDraggingSelection;
        private Point _selectionStart;
        private Point _selectionCurrent;

        // Automatically add message filter so we can globally catch Ctrl+RightClick
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Application.AddMessageFilter(this);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_RBUTTONDOWN)
            {
                if (Control.ModifierKeys.HasFlag(Keys.Control))
                {
                    var screenPoint = Control.MousePosition;
                    var clientPoint = MapControl.PointToClient(screenPoint);
                    if (MapControl.ClientRectangle.Contains(clientPoint))
                    {
                        _isSelectingArea = true;
                        _isDraggingSelection = true;
                        _selectionStart = screenPoint;
                        _selectionCurrent = screenPoint;
                        Cursor = Cursors.Cross;
                        ControlPaint.DrawReversibleFrame(GetSelectionRectangle(), Color.Black, FrameStyle.Dashed);
                        return true; // Consume event
                    }
                }
            }
            else if (m.Msg == WM_MOUSEMOVE && _isDraggingSelection)
            {
                var screenPoint = Control.MousePosition;
                // Erase old
                ControlPaint.DrawReversibleFrame(GetSelectionRectangle(), Color.Black, FrameStyle.Dashed);
                _selectionCurrent = screenPoint;
                // Draw new
                ControlPaint.DrawReversibleFrame(GetSelectionRectangle(), Color.Black, FrameStyle.Dashed);
                return true;
            }
            else if (m.Msg == WM_RBUTTONUP && _isDraggingSelection)
            {
                var screenPoint = Control.MousePosition;
                // Erase old
                ControlPaint.DrawReversibleFrame(GetSelectionRectangle(), Color.Black, FrameStyle.Dashed);
                _isDraggingSelection = false;
                _isSelectingArea = false;
                Cursor = Cursors.Default;
                
                FinishAreaSelection();
                return true;
            }

            return false;
        }

        private Rectangle GetSelectionRectangle()
        {
            return new Rectangle(
                Math.Min(_selectionStart.X, _selectionCurrent.X),
                Math.Min(_selectionStart.Y, _selectionCurrent.Y),
                Math.Abs(_selectionStart.X - _selectionCurrent.X),
                Math.Abs(_selectionStart.Y - _selectionCurrent.Y)
            );
        }

        private void FinishAreaSelection()
        {
            var p1 = MapControl.PointToClient(_selectionStart);
            var p2 = MapControl.PointToClient(_selectionCurrent);
            
            var w1 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p1.X, p1.Y);
            var w2 = MapControl.Map.Navigator.Viewport.ScreenToWorld(p2.X, p2.Y);

            var (lat1, lon1) = Helpers.CoordinateHelper.ToWgs84(w1.X, w1.Y);
            var (lat2, lon2) = Helpers.CoordinateHelper.ToWgs84(w2.X, w2.Y);
            
            var rect = GetSelectionRectangle();

            SendEvent(new Protocol.AreaSelectedEvent
            {
                MinLat = Math.Min(lat1, lat2),
                MinLon = Math.Min(lon1, lon2),
                MaxLat = Math.Max(lat1, lat2),
                MaxLon = Math.Max(lon1, lon2),
                ScreenX = rect.X,
                ScreenY = rect.Y,
                ScreenWidth = rect.Width,
                ScreenHeight = rect.Height
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pipeServer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
