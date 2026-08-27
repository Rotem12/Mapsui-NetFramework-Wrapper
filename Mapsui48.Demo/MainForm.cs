using Mapsui48.Client;
using Mapsui48.Protocol;
using PelcoControlNM;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mapsui48.Demo
{
    public partial class MainForm : Form
    {
        private MapHostPanel _mapPanel;
        private FloatingTooltipForm _tooltipForm;
        private PelcoControl _pelco;
        private frmMap _frmMapInstance;
        private TelemetrySimulator _simulator;
        private ElevationManager _elevationManager;

        public double InstallHeight { get; set; } = 15;

        private class TargetInfo
        {
            public string FeatureId { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public bool IsHighlighted { get; set; }
        }

        private Dictionary<string, TargetInfo> Targets = new Dictionary<string, TargetInfo>();

        public MainForm()
        {
            InitializeComponent();
            _tooltipForm = new FloatingTooltipForm();
            _pelco = new PelcoControl();
            _simulator = new TelemetrySimulator(_pelco);
            _elevationManager = new ElevationManager(@"C:\MapCache\Elevations", "cca51971858ebe853218ee20d8b78191", ElevationMode.CacheOnly);
            InitializeMap();
        }

        private void InitializeMap()
        {
            _mapPanel = new MapHostPanel
            {
                Dock = DockStyle.Fill,
                // You can configure MBTilesPath here if you have one
                MBTilesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "somemap1.mbtiles"),
                OnlineUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                CachePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mapsui48Demo", "Cache")
            };

            _mapPanel.MapClicked += MapPanel_MapClicked;
            _mapPanel.FeatureClicked += MapPanel_FeatureClicked;
            _mapPanel.ViewportChanged += MapPanel_ViewportChanged;
            
            // Navigate directly to land on startup (Jerusalem region) and render initial camera marker & vision cone
            _mapPanel.HostReady += async (s, e) =>
            {
                await _mapPanel.NavigateToAsync(_pelco.Latitude, _pelco.Longitude, zoom: 12);
                await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, 15.0);
            };

            // Hook live PointerMoved and PointerLeft events for the Custom Tooltip Test
            _mapPanel.PointerMoved += MapPanel_PointerMoved;
            _mapPanel.PointerLeft += (s, e) => { if (!IsDisposed) Invoke((MethodInvoker)delegate { _tooltipForm?.Hide(); }); };

            panelMapContainer.Controls.Add(_mapPanel);

            InitializeProviderDropdown();
            InitializeIconTestControls();
            InitializePelcoControls();
            InitializeTelemetrySimulatorControls();
            InitializeStyleContextMenu();
            InitializeContextMenuOverlay();
        }

        private void MapPanel_PointerMoved(object sender, MapPointerMovedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                if (_styleContextMenu != null && _styleContextMenu.Visible)
                {
                    _tooltipForm?.Hide();
                    return;
                }

                // Update the floating tooltip positioned next to the cursor
                var screenPoint = Cursor.Position;
                _tooltipForm.UpdateTooltip($"Lat: {e.Latitude:F5}\nLon: {e.Longitude:F5}", screenPoint);

                // Also display in the status bar
                lblStatus.Text = $"Cursor: {e.Latitude:F5}, {e.Longitude:F5} (X:{e.ScreenX:0}, Y:{e.ScreenY:0})";
            });
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _tooltipForm?.Hide();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _tooltipForm?.Dispose();
            base.OnFormClosed(e);
        }

        private class ProviderItem
        {
            public string Name { get; set; }
            public string Url { get; set; }
        }

        private void InitializeProviderDropdown()
        {
            var cmbProvider = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Location = new System.Drawing.Point(540, 12)
            };

            cmbProvider.Items.Add(new ProviderItem { Name = "OpenStreetMap", Url = "https://tile.openstreetmap.org/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Offline Only (No Online Map)", Url = "" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Google Map", Url = "https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Google Satellite", Url = "https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Google Hybrid", Url = "https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Google Terrain", Url = "https://mt1.google.com/vt/lyrs=p&x={x}&y={y}&z={z}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Bing Aerial", Url = "Known:BingAerial" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Bing Hybrid", Url = "Known:BingHybrid" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Bing Roads", Url = "Known:BingRoads" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Carto Light", Url = "https://cartodb-basemaps-a.global.ssl.fastly.net/light_all/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Carto Dark", Url = "https://cartodb-basemaps-a.global.ssl.fastly.net/dark_all/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Esri World Street", Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Esri World Satellite", Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Esri World Topo", Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}" });
            
            cmbProvider.DisplayMember = "Name";
            cmbProvider.ValueMember = "Url";
            cmbProvider.SelectedIndex = 0;

            cmbProvider.SelectedIndexChanged += async (s, e) =>
            {
                if (cmbProvider.SelectedItem is ProviderItem selected)
                {
                    await _mapPanel.ChangeOnlineProviderAsync(selected.Url);
                }
            };

            panelTop.Controls.Add(cmbProvider);

            // Setup the Elevation Manager hook
            _mapPanel.OnDownloadStarted += async (bbox, minZ, maxZ, token) =>
            {
                // This is where the Elevation Manager hooks in
                Console.WriteLine($"[ElevationManager] Hook triggered for bbox {bbox.MinLat},{bbox.MinLon} to {bbox.MaxLat},{bbox.MaxLon}");
                Console.WriteLine($"[ElevationManager] Downloading elevation data for zooms {minZ}-{maxZ}...");
                
                // Simulate some work
                for(int i=0; i<10; i++)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(200, token);
                }
                Console.WriteLine("[ElevationManager] Elevation data download complete.");
            };
        }

        private class DropdownItem<T>
        {
            public string Text { get; set; }
            public T Value { get; set; }
            public override string ToString() => Text;
        }

        private void InitializeIconTestControls()
        {
            var lblIcon = new Label { Text = "Icon:", AutoSize = true, Location = new System.Drawing.Point(10, 48) };
            var cmbIcons = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Location = new System.Drawing.Point(48, 45)
            };
            foreach (var icon in MapIconCatalog.GetAllIcons())
            {
                cmbIcons.Items.Add(new DropdownItem<string> { Text = $"{icon.Category} - {icon.Name}", Value = icon.Key });
            }
            cmbIcons.SelectedIndex = 0;

            var lblColor = new Label { Text = "Color:", AutoSize = true, Location = new System.Drawing.Point(255, 48) };
            var cmbColors = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Location = new System.Drawing.Point(295, 45)
            };
            foreach (var col in MapIconCatalog.StandardColors)
            {
                cmbColors.Items.Add(new DropdownItem<string> { Text = col.Name, Value = col.Hex });
            }
            cmbColors.SelectedIndex = 0;

            var lblRot = new Label { Text = "Rot:", AutoSize = true, Location = new System.Drawing.Point(422, 48) };
            var numRotation = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 360,
                Value = 0,
                Width = 50,
                Location = new System.Drawing.Point(452, 45)
            };

            var btnPlace = new Button
            {
                Text = "Place Icon",
                Width = 85,
                Height = 25,
                Location = new System.Drawing.Point(510, 44)
            };

            var btnDemoGrid = new Button
            {
                Text = "Demo All 24 Icons",
                Width = 140,
                Height = 25,
                Location = new System.Drawing.Point(602, 44)
            };

            var lblAnim = new Label { Text = "Anim:", AutoSize = true, Location = new System.Drawing.Point(748, 48) };
            var cmbAnim = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 145,
                Location = new System.Drawing.Point(788, 45)
            };
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Elevation 3D", Value = IconAnimationType.Elevation3D });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Compass Bearing", Value = IconAnimationType.CompassBearing });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Thermal IR Scan", Value = IconAnimationType.ThermalScan });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Laser Rangefinder", Value = IconAnimationType.LaserRangefinder });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Missile Trajectory", Value = IconAnimationType.MissileTrajectory });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Orbit Satellite", Value = IconAnimationType.OrbitSatellite });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Shield Defense", Value = IconAnimationType.ShieldDefense });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Sniper Scope Zoom", Value = IconAnimationType.SniperScope });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Helipad LZ Rotor", Value = IconAnimationType.HelipadLZ });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Radar Sweep", Value = IconAnimationType.RadarSweep });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Sonar Wave", Value = IconAnimationType.SonarWave });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Crosshair Lock", Value = IconAnimationType.CrosshairLock });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Strobe Alert", Value = IconAnimationType.StrobeAlert });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Flight Horizon", Value = IconAnimationType.FlightHorizon });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Maritime Wake", Value = IconAnimationType.MaritimeWake });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Flag Waving", Value = IconAnimationType.FlagWaving });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Flame Hotspot", Value = IconAnimationType.FlamePulse });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Pulse Aura", Value = IconAnimationType.Pulse });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Rotate 360", Value = IconAnimationType.Rotate });
            cmbAnim.Items.Add(new DropdownItem<IconAnimationType> { Text = "Bounce Bob", Value = IconAnimationType.Bounce });
            cmbAnim.SelectedIndex = 0;

            var btnAddMenuBtn = new Button
            {
                Text = "+ Add to Right-Click Menu",
                Width = 160,
                Height = 25,
                Location = new System.Drawing.Point(940, 44),
                BackColor = Color.FromArgb(235, 240, 255),
                Cursor = Cursors.Hand
            };

            double lastLat = 0, lastLon = 0;
            _mapPanel.ViewportChanged += (s, e) =>
            {
                lastLat = e.CenterLat;
                lastLon = e.CenterLon;
            };

            btnPlace.Click += async (s, e) =>
            {
                if (cmbIcons.SelectedItem is DropdownItem<string> selIcon && cmbColors.SelectedItem is DropdownItem<string> selColor)
                {
                    string iconKey = selIcon.Value;
                    string colorHex = selColor.Value;
                    double rot = (double)numRotation.Value;
                    await _mapPanel.AddPointAsync("Targets", lastLat, lastLon, label: selIcon.Text, color: colorHex, scale: 1.3, rotation: rot, iconType: iconKey);
                }
            };

            btnDemoGrid.Click += async (s, e) =>
            {
                await _mapPanel.ClearLayerAsync("Targets");
                var allIcons = MapIconCatalog.GetAllIcons();
                var cols = MapIconCatalog.StandardColors;

                int colsCount = 6;
                double stepLat = 0.008;
                double stepLon = 0.012;

                for (int i = 0; i < allIcons.Count; i++)
                {
                    int row = i / colsCount;
                    int col = i % colsCount;

                    double lat = lastLat + (row - 1.5) * stepLat;
                    double lon = lastLon + (col - 2.5) * stepLon;
                    var color = cols[i % cols.Count].Hex;

                    await _mapPanel.AddPointAsync("Targets", lat, lon, label: allIcons[i].Name, color: color, scale: 1.2, rotation: (i * 30) % 360, iconType: allIcons[i].Key);
                }
            };

            btnAddMenuBtn.Click += (s, e) =>
            {
                if (cmbIcons.SelectedItem is DropdownItem<string> selIcon &&
                    cmbColors.SelectedItem is DropdownItem<string> selColor &&
                    cmbAnim.SelectedItem is DropdownItem<IconAnimationType> selAnim)
                {
                    string iconKey = selIcon.Value;
                    string colorHex = selColor.Value;
                    var anim = selAnim.Value;
                    var parsedColor = ColorTranslator.FromHtml(colorHex);

                    string buttonName = selIcon.Text.Contains("-") ? selIcon.Text.Split('-')[1].Trim() : selIcon.Text;

                    var newItem = new MapContextMenuItem
                    {
                        Name = buttonName,
                        Tooltip = $"{buttonName}\nAnimation: {selAnim.Text} | Color: {selColor.Text}",
                        IconKey = iconKey,
                        AnimationType = anim,
                        AccentColor = parsedColor,
                        OnClick = async (ctx) =>
                        {
                            lblStatus.Text = $"Action: '{buttonName}' at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                            await _mapPanel.AddPointAsync("Targets", ctx.Latitude, ctx.Longitude, label: buttonName, color: colorHex, scale: 1.3, iconType: iconKey);
                        }
                    };

                    _mapPanel.ContextMenuOverlay.AddItem(newItem);
                    lblStatus.Text = $"[Menu Updated] Added '{buttonName}' ({selAnim.Text}) to Right-Click Menu! Right-click on map to test.";
                }
            };

            panelTop.Controls.Add(lblIcon);
            panelTop.Controls.Add(cmbIcons);
            panelTop.Controls.Add(lblColor);
            panelTop.Controls.Add(cmbColors);
            panelTop.Controls.Add(lblRot);
            panelTop.Controls.Add(numRotation);
            panelTop.Controls.Add(btnPlace);
            panelTop.Controls.Add(btnDemoGrid);
            panelTop.Controls.Add(lblAnim);
            panelTop.Controls.Add(cmbAnim);
            panelTop.Controls.Add(btnAddMenuBtn);
        }

        private void InitializePelcoControls()
        {
            // Row 3: Pelco PTZ Live Simulator Controls (Y = 80)
            var btnLaunchFrmMap = new Button
            {
                Text = "🎯 Launch frmMap (Pelco)",
                Width = 175,
                Height = 28,
                Location = new System.Drawing.Point(10, 80),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };

            var lblPtz = new Label { Text = "PTZ Cam:", AutoSize = true, Location = new System.Drawing.Point(195, 85), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            var lblPan = new Label { Text = "Pan:", AutoSize = true, Location = new System.Drawing.Point(265, 85) };
            var numPan = new NumericUpDown { Minimum = 0, Maximum = 360, Value = (decimal)_pelco.CurrentPanNorthed, Width = 55, Location = new System.Drawing.Point(295, 83) };

            var lblTilt = new Label { Text = "Tilt:", AutoSize = true, Location = new System.Drawing.Point(360, 85) };
            var numTilt = new NumericUpDown { Minimum = -90, Maximum = 90, Value = (decimal)_pelco.CurrentTilt, Width = 55, Location = new System.Drawing.Point(390, 83) };

            var lblZoom = new Label { Text = "FOV:", AutoSize = true, Location = new System.Drawing.Point(455, 85) };
            var numZoom = new NumericUpDown { Minimum = 2, Maximum = 120, Value = (decimal)_pelco.CurrentZoom, Width = 55, Location = new System.Drawing.Point(485, 83) };

            var lblElev = new Label { Text = "Elev:", AutoSize = true, Location = new System.Drawing.Point(550, 85) };
            var numElev = new NumericUpDown { Minimum = 0, Maximum = 500, Value = 15, Width = 50, Location = new System.Drawing.Point(585, 83) };

            var btnRotate = new Button
            {
                Text = "Update FOV Cone",
                Width = 125,
                Height = 26,
                Location = new System.Drawing.Point(645, 82),
                BackColor = Color.FromArgb(235, 245, 255),
                Cursor = Cursors.Hand
            };

            var lblPelcoStatus = new Label
            {
                Text = "Pelco PTZ: Ready. Click 'Launch frmMap' to test.",
                AutoSize = true,
                Location = new System.Drawing.Point(780, 86),
                ForeColor = Color.FromArgb(0, 100, 200),
                Font = new Font("Segoe UI", 9f, FontStyle.Italic)
            };

            // Event Handlers
            btnLaunchFrmMap.Click += (s, e) =>
            {
                if (_frmMapInstance == null || _frmMapInstance.IsDisposed)
                {
                    _frmMapInstance = new frmMap(_pelco);
                    _frmMapInstance.InstallHeight = (int)numElev.Value;
                    _frmMapInstance.Show();
                    lblPelcoStatus.Text = "frmMap window opened.";
                }
                else
                {
                    _frmMapInstance.BringToFront();
                }
            };

            Action updateCamera = async () =>
            {
                _pelco.CurrentPanNorthed = (double)numPan.Value;
                _pelco.CurrentTilt = (double)numTilt.Value;
                _pelco.CurrentZoom = (double)numZoom.Value;
                if (_frmMapInstance != null && !_frmMapInstance.IsDisposed)
                {
                    _frmMapInstance.InstallHeight = (int)numElev.Value;
                    _ = _frmMapInstance.Rotate();
                }
                await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, (double)numElev.Value);
            };

            numPan.ValueChanged += (s, e) => updateCamera();
            numTilt.ValueChanged += (s, e) => updateCamera();
            numZoom.ValueChanged += (s, e) => updateCamera();
            numElev.ValueChanged += (s, e) => updateCamera();
            btnRotate.Click += (s, e) => updateCamera();

            // When frmMap executes Goto (from right-click Goto with elevation or bearing)
            _pelco.GotoExecuted += (pan, tilt, sendFast) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                Invoke((MethodInvoker)async delegate
                {
                    numPan.Value = (decimal)Math.Round(pan, 1);
                    numTilt.Value = (decimal)Math.Round(tilt, 1);
                    lblPelcoStatus.Text = $"[Goto Received] Pan={pan:F1}°, Tilt={tilt:F1}° (Fast={sendFast})";
                    await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, pan, tilt, (double)numZoom.Value, (double)numElev.Value);
                });
            };

            panelTop.Controls.Add(btnLaunchFrmMap);
            panelTop.Controls.Add(lblPtz);
            panelTop.Controls.Add(lblPan);
            panelTop.Controls.Add(numPan);
            panelTop.Controls.Add(lblTilt);
            panelTop.Controls.Add(numTilt);
            panelTop.Controls.Add(lblZoom);
            panelTop.Controls.Add(numZoom);
            panelTop.Controls.Add(lblElev);
            panelTop.Controls.Add(numElev);
            panelTop.Controls.Add(btnRotate);
            panelTop.Controls.Add(lblPelcoStatus);
        }

        private void InitializeTelemetrySimulatorControls()
        {
            // Row 4: External GPS & PTZ Telemetry Simulator (Y = 118)
            var lblSimTitle = new Label
            {
                Text = "📡 Ext Feed:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 122),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 120, 200)
            };

            var cmbRoute = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Location = new System.Drawing.Point(90, 118)
            };
            cmbRoute.Items.Add(new DropdownItem<SimulationRoute> { Text = "🚁 UAV Orbit (Jerusalem R=1.2km)", Value = SimulationRoute.DroneOrbit });
            cmbRoute.Items.Add(new DropdownItem<SimulationRoute> { Text = "🎯 Target Lock-On Orbit (Continuous Lock)", Value = SimulationRoute.TargetLockOrbit });
            cmbRoute.Items.Add(new DropdownItem<SimulationRoute> { Text = "🗼 360° Tower Surveillance Sweep", Value = SimulationRoute.SurveillanceSweep360 });
            cmbRoute.Items.Add(new DropdownItem<SimulationRoute> { Text = "🚙 Patrol Vehicle (Tel Aviv Corridor)", Value = SimulationRoute.LinearPatrol });
            cmbRoute.Items.Add(new DropdownItem<SimulationRoute> { Text = "🏔 Mountain Flight Recon (Hermon)", Value = SimulationRoute.MountainFlight });
            cmbRoute.SelectedIndex = 0;

            var btnSimToggle = new Button
            {
                Text = "▶ Start GPS Stream",
                Width = 140,
                Height = 26,
                Location = new System.Drawing.Point(318, 117),
                BackColor = Color.FromArgb(34, 197, 94), // Green
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };

            var cmbSpeed = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 90,
                Location = new System.Drawing.Point(465, 118)
            };
            cmbSpeed.Items.Add(new DropdownItem<double> { Text = "1x Speed", Value = 1.0 });
            cmbSpeed.Items.Add(new DropdownItem<double> { Text = "2x Speed", Value = 2.0 });
            cmbSpeed.Items.Add(new DropdownItem<double> { Text = "5x Speed", Value = 5.0 });
            cmbSpeed.SelectedIndex = 0;

            var chkFollowCamera = new CheckBox
            {
                Text = "Follow Viewport",
                AutoSize = true,
                Location = new System.Drawing.Point(565, 121),
                Checked = false
            };

            var lblNmeaTicker = new Label
            {
                Text = "GPS Telemetry: Idle. Click 'Start GPS Stream' to simulate live camera reporting.",
                AutoSize = true,
                Location = new System.Drawing.Point(680, 122),
                Font = new Font("Consolas", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            // Event Handlers
            cmbRoute.SelectedIndexChanged += (s, e) =>
            {
                if (cmbRoute.SelectedItem is DropdownItem<SimulationRoute> sel)
                {
                    _simulator.Route = sel.Value;
                }
            };

            cmbSpeed.SelectedIndexChanged += (s, e) =>
            {
                if (cmbSpeed.SelectedItem is DropdownItem<double> sel)
                {
                    _simulator.SpeedMultiplier = sel.Value;
                }
            };

            chkFollowCamera.CheckedChanged += (s, e) =>
            {
                if (_frmMapInstance != null && !_frmMapInstance.IsDisposed)
                {
                    _frmMapInstance.FollowCamera = chkFollowCamera.Checked;
                }
            };

            btnSimToggle.Click += (s, e) =>
            {
                if (_simulator.IsRunning)
                {
                    _simulator.Stop();
                    btnSimToggle.Text = "▶ Start GPS Stream";
                    btnSimToggle.BackColor = Color.FromArgb(34, 197, 94); // Green
                    lblNmeaTicker.Text = "GPS Telemetry Stream: Paused.";
                }
                else
                {
                    _simulator.Start();
                    btnSimToggle.Text = "⏹ Stop Stream";
                    btnSimToggle.BackColor = Color.FromArgb(239, 68, 68); // Red
                    lblNmeaTicker.Text = "GPS Telemetry Stream: Live streaming...";
                }
            };

            // Hook live incoming packets from external simulation
            _simulator.TelemetryUpdated += (packet) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                Invoke((MethodInvoker)async delegate
                {
                    lblNmeaTicker.Text = $"[GPS/PTZ] Lat: {packet.Latitude:F5} Lon: {packet.Longitude:F5} Alt: {packet.AltitudeMeters:F0}m | Pan: {packet.Pan:F1}° Tilt: {packet.Tilt:F1}° FOV: {packet.Zoom:F0}° | {packet.Description}";

                    // Also display incoming NMEA sentence in main status bar
                    lblStatus.Text = $"{packet.RawNmea} | Speed: {packet.GroundSpeedKmh:F0} km/h | Heading: {packet.Heading:F0}°";

                    if (_mapPanel != null && _mapPanel.IsHostReady)
                    {
                        // Render moving simulated camera marker and vision cone on MainForm map
                        await UpdateMainFormCameraDisplay(packet.Latitude, packet.Longitude, packet.Pan, packet.Tilt, packet.Zoom, packet.AltitudeMeters);

                        if (chkFollowCamera.Checked)
                        {
                            await _mapPanel.NavigateToAsync(packet.Latitude, packet.Longitude, _mapPanel.CurrentZoom > 1 ? _mapPanel.CurrentZoom : 13);
                        }
                    }
                });
            };

            panelTop.Controls.Add(lblSimTitle);
            panelTop.Controls.Add(cmbRoute);
            panelTop.Controls.Add(btnSimToggle);
            panelTop.Controls.Add(cmbSpeed);
            panelTop.Controls.Add(chkFollowCamera);
            panelTop.Controls.Add(lblNmeaTicker);
        }

        private async Task UpdateMainFormCameraDisplay(double lat, double lon, double pan, double tilt, double fov, double alt)
        {
            if (_mapPanel == null || !_mapPanel.IsHostReady) return;

            // 1. Render camera marker
            await _mapPanel.AddPointAsync("Camera", lat, lon, label: "Camera", color: _cameraColor, scale: 1.2, rotation: pan, iconType: _cameraIconType, featureId: "MainCamMarker");

            double groundAlt = _elevationManager != null ? _elevationManager.GetElevation(lat, lon) : 0;
            double camAbsoluteAlt = groundAlt + alt;

            // 2. Render vision cone with elevation terrain occlusion
            double[][] coords = frmMap.CreateVisionTriangleStatic(lat, lon, camAbsoluteAlt, pan, tilt, fov, vfov: null, stepMeters: 25, maxRangeMeters: 15000, elevMgr: _elevationManager);
            if (coords != null && coords.Length >= 3)
            {
                await _mapPanel.AddPolygonAsync("Vision", coords, fillColor: "#3C8B0000", outlineColor: "#8B0000", outlineWidth: 1.5, featureId: "MainCamVision");
            }
            else
            {
                await _mapPanel.ClearLayerAsync("Vision");
            }
        }

        private string _cameraIconType = "circle";
        private string _targetIconType = "crosshair";
        private string _cameraColor = "#FF0000";
        private string _targetColor = "#FF4500";
        private ContextMenuStrip _styleContextMenu;

        private void InitializeStyleContextMenu()
        {
            _styleContextMenu = new ContextMenuStrip();

            // 1. Target Icon Submenu
            var miTargetIcon = new ToolStripMenuItem("Target Icon");
            foreach (var icon in MapIconCatalog.GetAllIcons())
            {
                var item = new ToolStripMenuItem(icon.Name) { Tag = icon.Key };
                item.Click += async (s, e) =>
                {
                    _targetIconType = (string)item.Tag;
                    lblStatus.Text = $"Target icon set to: {icon.Name}";
                    await RefreshAllTargets();
                };
                miTargetIcon.DropDownItems.Add(item);
            }
            _styleContextMenu.Items.Add(miTargetIcon);

            // 2. Target Color Submenu
            var miTargetColor = new ToolStripMenuItem("Target Color");
            foreach (var color in MapIconCatalog.StandardColors)
            {
                var item = new ToolStripMenuItem(color.Name) { Tag = color.Hex };
                item.Click += async (s, e) =>
                {
                    _targetColor = (string)item.Tag;
                    lblStatus.Text = $"Target color set to: {color.Name}";
                    await RefreshAllTargets();
                };
                miTargetColor.DropDownItems.Add(item);
            }
            _styleContextMenu.Items.Add(miTargetColor);

            // 3. Camera Icon Submenu
            var miCameraIcon = new ToolStripMenuItem("Camera Icon");
            foreach (var icon in MapIconCatalog.GetAllIcons())
            {
                var item = new ToolStripMenuItem(icon.Name) { Tag = icon.Key };
                item.Click += async (s, e) =>
                {
                    _cameraIconType = (string)item.Tag;
                    lblStatus.Text = $"Camera icon set to: {icon.Name}";
                    await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                };
                miCameraIcon.DropDownItems.Add(item);
            }
            _styleContextMenu.Items.Add(miCameraIcon);

            // 4. Camera Color Submenu
            var miCameraColor = new ToolStripMenuItem("Camera Color");
            foreach (var color in MapIconCatalog.StandardColors)
            {
                var item = new ToolStripMenuItem(color.Name) { Tag = color.Hex };
                item.Click += async (s, e) =>
                {
                    _cameraColor = (string)item.Tag;
                    lblStatus.Text = $"Camera color set to: {color.Name}";
                    await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                };
                miCameraColor.DropDownItems.Add(item);
            }
            _styleContextMenu.Items.Add(miCameraColor);

            ApplyDimGrayToMenu(_styleContextMenu);

            // Wire bottom-left circular style button on map
            _mapPanel.StyleButtonClicked += (s, e) =>
            {
                if (_styleContextMenu != null && !_styleContextMenu.IsDisposed)
                {
                    _tooltipForm?.Hide();
                    Point screenPt = _mapPanel.PointToScreen(new Point(10, _mapPanel.Height - 46));
                    Point formPt = this.PointToClient(screenPt);
                    _styleContextMenu.Show(this, formPt, ToolStripDropDownDirection.AboveRight);
                }
            };
        }

        private static void ApplyDimGrayToMenu(ContextMenuStrip menu)
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new DimGrayColorTable());
            menu.BackColor = Color.DimGray;
            menu.ForeColor = Color.White;
            menu.ShowImageMargin = false;

            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = Color.DimGray;
                item.ForeColor = Color.White;
                if (item is ToolStripMenuItem mi)
                {
                    mi.DropDown.BackColor = Color.DimGray;
                    mi.DropDown.ForeColor = Color.White;
                    mi.DropDown.Renderer = new ToolStripProfessionalRenderer(new DimGrayColorTable());
                    if (mi.DropDown is ToolStripDropDownMenu dropDownMenu)
                    {
                        dropDownMenu.ShowImageMargin = false;
                    }
                    foreach (ToolStripItem sub in mi.DropDownItems)
                    {
                        sub.BackColor = Color.DimGray;
                        sub.ForeColor = Color.White;
                    }
                }
            }
        }

        private double _currentZoom = 13.0;

        private void MapPanel_ViewportChanged(object sender, ViewportChangedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            _currentZoom = e.ZoomLevel;
            Invoke((MethodInvoker)delegate
            {
                lblStatus.Text = $"Center: {e.CenterLat:F4}, {e.CenterLon:F4} | Zoom: {e.ZoomLevel:F1}";
            });
        }

        private void MapPanel_FeatureClicked(object sender, FeatureClickedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                MessageBox.Show($"Clicked feature {e.FeatureId} on layer {e.LayerName} at {e.Latitude:F4}, {e.Longitude:F4}", "Feature Clicked");
            });
        }

        private void MapPanel_MapClicked(object sender, MapClickedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                Console.WriteLine($"Map clicked at {e.Latitude:F4}, {e.Longitude:F4}");
            });
        }

        private async void btnNavigate_Click(object sender, EventArgs e)
        {
            await _mapPanel.GoHomeAsync(durationMs: 1500);
        }

        private async void btnFly_Click(object sender, EventArgs e)
        {
            await _mapPanel.FlyToAsync(40.7128, -74.0060, zoom: 14, durationMs: 2000);
        }

        private async void btnAddPolygon_Click(object sender, EventArgs e)
        {
            try
            {
                await _mapPanel.AddPolygonAsync("Zones", new[]
                {
                    new[] { -1.0, -1.0 },
                    new[] { 1.0, -1.0 },
                    new[] { 1.0, 1.0 },
                    new[] { -1.0, 1.0 },
                    new[] { -1.0, -1.0 }
                }, fillColor: "#80FF0000", outlineColor: "#FFFF0000", outlineWidth: 3);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void btnAddPoint_Click(object sender, EventArgs e)
        {
            try
            {
                await AddTargetAt(_pelco.Latitude + 0.005, _pelco.Longitude + 0.005);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                await _mapPanel.ClearLayerAsync("Zones");
                await _mapPanel.ClearLayerAsync("Targets");
                Targets.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ==========================================
        // Target Tracking Management & Helpers
        // ==========================================

        public async Task<string> AddTargetAt(double lat, double lon, string name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetNextTargetName();
            }
            await AddTarget(name, new double[] { lat, lon });
            return name;
        }

        public async Task AddTarget(string name, double[] target, string iconType = null, string color = null)
        {
            if (target == null || target.Length < 2) return;

            if (Targets.TryGetValue(name, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.FeatureId))
                    await _mapPanel.RemoveFeatureAsync("Targets", existing.FeatureId);
            }

            string id = await _mapPanel.AddPointAsync("Targets", target[0], target[1], label: name, color: color ?? _targetColor, scale: 1.2, iconType: iconType ?? _targetIconType);
            Targets[name] = new TargetInfo { FeatureId = id, Lat = target[0], Lon = target[1], IsHighlighted = false };
            lblStatus.Text = $"Added target '{name}' at {target[0]:F5}, {target[1]:F5}";
        }

        public async Task DeleteTarget(string name)
        {
            if (Targets.TryGetValue(name, out var t))
            {
                Targets.Remove(name);
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await _mapPanel.RemoveFeatureAsync("Targets", t.FeatureId);
                }
                lblStatus.Text = $"Deleted target '{name}'";
            }
        }

        public async Task<bool> RenameTarget(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return false;
            if (!Targets.TryGetValue(oldName, out var t)) return false;
            if (Targets.ContainsKey(newName))
            {
                MessageBox.Show($"A target named '{newName}' already exists.", "Rename Target", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            Targets.Remove(oldName);
            if (!string.IsNullOrEmpty(t.FeatureId))
            {
                await _mapPanel.RemoveFeatureAsync("Targets", t.FeatureId);
            }

            string col = t.IsHighlighted ? "#00E5FF" : _targetColor;
            t.FeatureId = await _mapPanel.AddPointAsync("Targets", t.Lat, t.Lon, label: newName, color: col, scale: 1.2, iconType: _targetIconType);
            Targets[newName] = t;
            lblStatus.Text = $"Renamed target '{oldName}' to '{newName}'";
            return true;
        }

        private async Task RefreshAllTargets()
        {
            foreach (var kvp in Targets.ToList())
            {
                var t = kvp.Value;
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await _mapPanel.RemoveFeatureAsync("Targets", t.FeatureId);
                }
                string col = t.IsHighlighted ? "#00E5FF" : _targetColor;
                double sc = t.IsHighlighted ? 1.5 : 1.2;
                t.FeatureId = await _mapPanel.AddPointAsync("Targets", t.Lat, t.Lon, label: kvp.Key, color: col, scale: sc, iconType: _targetIconType);
            }
        }

        private (string Name, TargetInfo Info)? GetTargetAt(double lat, double lon, double maxPixelTolerance = 18)
        {
            double currentZoom = _currentZoom > 0 ? _currentZoom : 13;
            double latRad = lat * Math.PI / 180.0;
            // Standard Spherical Mercator meters per pixel at latitude
            double metersPerPixel = (156543.03392804097 * Math.Cos(latRad)) / Math.Pow(2, currentZoom);
            double maxDistanceKm = (maxPixelTolerance * metersPerPixel) / 1000.0;

            (string Name, TargetInfo Info)? closest = null;
            double minDistanceKm = double.MaxValue;

            foreach (var kvp in Targets)
            {
                double distKm = DistanceKm(lat, lon, kvp.Value.Lat, kvp.Value.Lon);
                if (distKm <= maxDistanceKm && distKm < minDistanceKm)
                {
                    minDistanceKm = distKm;
                    closest = (kvp.Key, kvp.Value);
                }
            }

            return closest;
        }

        private string GetNextTargetName()
        {
            int index = 1;
            while (Targets.ContainsKey($"Target {index}"))
            {
                index++;
            }
            return $"Target {index}";
        }

        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 320;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = caption;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.ShowInTaskbar = false;
                prompt.BackColor = Color.DimGray;
                prompt.ForeColor = Color.White;

                Label textLabel = new Label() { Left = 16, Top = 16, Width = 270, Text = text, ForeColor = Color.White };
                TextBox textBox = new TextBox()
                {
                    Left = 16,
                    Top = 42,
                    Width = 270,
                    Text = defaultValue,
                    BackColor = Color.FromArgb(80, 80, 80),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                Button confirmation = new Button()
                {
                    Text = "OK",
                    Left = 126,
                    Width = 75,
                    Top = 80,
                    DialogResult = DialogResult.OK,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(70, 70, 70),
                    ForeColor = Color.White
                };
                confirmation.FlatAppearance.BorderSize = 0;
                Button cancel = new Button()
                {
                    Text = "Cancel",
                    Left = 211,
                    Width = 75,
                    Top = 80,
                    DialogResult = DialogResult.Cancel,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                cancel.FlatAppearance.BorderSize = 0;

                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(cancel);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }

        public (double Bearing, double Pitch) GetAngles(double camLat, double camLon, double camAlt, double tgtLat, double tgtLon, double tgtAlt)
        {
            double bearing = CalculateBearing(camLat, camLon, tgtLat, tgtLon);
            double distMeters = DistanceKm(camLat, camLon, tgtLat, tgtLon) * 1000.0;
            double deltaAlt = tgtAlt - camAlt;
            double pitch = Math.Atan2(deltaAlt, Math.Max(1.0, distMeters)) * (180.0 / Math.PI);
            return (bearing, pitch);
        }

        private double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double rlat1 = lat1 * Math.PI / 180.0;
            double rlat2 = lat2 * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;

            double y = Math.Sin(dLon) * Math.Cos(rlat2);
            double x = Math.Cos(rlat1) * Math.Sin(rlat2) - Math.Sin(rlat1) * Math.Cos(rlat2) * Math.Cos(dLon);
            double brng = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (brng + 360.0) % 360.0;
        }

        private double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // ==========================================
        // Right Click Tactical Context Overlay (HUD)
        // ==========================================

        private void InitializeContextMenuOverlay()
        {
            _mapPanel.ContextMenuOpening -= MapPanel_ContextMenuOpening;
            _mapPanel.ContextMenuOpening += MapPanel_ContextMenuOpening;
        }

        private void MapPanel_ContextMenuOpening(object sender, ContextMenuOpeningEventArgs e)
        {
            var overlay = _mapPanel.ContextMenuOverlay;
            if (overlay == null) return;
            overlay.ClearItems();

            var targetHit = GetTargetAt(e.Latitude, e.Longitude);
            if (targetHit.HasValue)
            {
                var targetName = targetHit.Value.Name;
                var target = targetHit.Value.Info;

                // 1. Goto Target (With Elevation)
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto",
                    Tooltip = $"Goto '{targetName}' with elevation\nPan Azimuth and Tilt to target position",
                    IconKey = "goto_elevation",
                    AnimationType = IconAnimationType.Elevation3D,
                    AccentColor = Color.FromArgb(0, 229, 255), // Cyan
                    OnClick = async (ctx) =>
                    {
                        double tgtElev = _elevationManager != null ? _elevationManager.GetElevation(target.Lat, target.Lon) : 0;
                        double camAlt = (_elevationManager != null ? _elevationManager.GetElevation(_pelco.Latitude, _pelco.Longitude) : 0) + InstallHeight;
                        var angles = GetAngles(_pelco.Latitude, _pelco.Longitude, camAlt, target.Lat, target.Lon, tgtElev);

                        _pelco.GotoDeg(angles.Bearing + _pelco.AzimuthOffset, angles.Pitch + _pelco.ElevationOffset, false);
                        lblStatus.Text = $"Action: Goto '{targetName}' (Bearing: {angles.Bearing:F1}°, Pitch: {angles.Pitch:F1}°)";
                        await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                    }
                });

                // 2. Goto Target without elevation
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto (Bearing)",
                    Tooltip = $"Goto '{targetName}' without elevation\nPan Horizontal Azimuth only (Keep Current Tilt)",
                    IconKey = "goto_bearing",
                    AnimationType = IconAnimationType.CompassBearing,
                    AccentColor = Color.FromArgb(245, 158, 11), // Amber
                    OnClick = async (ctx) =>
                    {
                        var bearing = CalculateBearing(_pelco.Latitude, _pelco.Longitude, target.Lat, target.Lon);
                        double cameraAngle = (bearing + _pelco.AzimuthOffset + 360) % 360;

                        _pelco.GotoDeg(cameraAngle, _pelco.CurrentTilt, false);
                        lblStatus.Text = $"Action: Goto '{targetName}' Bearing only ({cameraAngle:F1}°)";
                        await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                    }
                });

                // 3. Rename Target
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Rename",
                    Tooltip = $"Rename '{targetName}'\nChange target label",
                    IconKey = "rename",
                    AnimationType = IconAnimationType.Pulse,
                    AccentColor = Color.FromArgb(56, 189, 248), // Sky Blue
                    OnClick = async (ctx) =>
                    {
                        string newName = ShowInputDialog($"Enter new name for target '{targetName}':", "Rename Target", targetName);
                        if (!string.IsNullOrWhiteSpace(newName) && newName != targetName)
                        {
                            await RenameTarget(targetName, newName);
                        }
                    }
                });

                // 4. Delete Target
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Delete",
                    Tooltip = $"Delete '{targetName}'\nRemove this target from map",
                    IconKey = "trash",
                    AnimationType = IconAnimationType.Pulse,
                    AccentColor = Color.FromArgb(239, 68, 68), // Red
                    OnClick = async (ctx) =>
                    {
                        await DeleteTarget(targetName);
                    }
                });
            }
            else
            {
                // Right-clicked on empty terrain
                // 1. Goto (With Elevation)
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto",
                    Tooltip = "Goto (With Elevation)\nCalculate Bearing & Pitch Angle using Elevation Data",
                    IconKey = "goto_elevation",
                    AnimationType = IconAnimationType.Elevation3D,
                    AccentColor = Color.FromArgb(0, 229, 255), // Cyan
                    OnClick = async (ctx) =>
                    {
                        double tgtElev = _elevationManager != null ? _elevationManager.GetElevation(ctx.Latitude, ctx.Longitude) : 0;
                        double camAlt = (_elevationManager != null ? _elevationManager.GetElevation(_pelco.Latitude, _pelco.Longitude) : 0) + InstallHeight;
                        var angles = GetAngles(_pelco.Latitude, _pelco.Longitude, camAlt, ctx.Latitude, ctx.Longitude, tgtElev);

                        _pelco.GotoDeg(angles.Bearing + _pelco.AzimuthOffset, angles.Pitch + _pelco.ElevationOffset, false);
                        lblStatus.Text = $"Action: Goto ({ctx.Latitude:F5}, {ctx.Longitude:F5}) with Elevation (Bearing: {angles.Bearing:F1}°, Pitch: {angles.Pitch:F1}°)";
                        await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                    }
                });

                // 2. Goto without elevation
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto without elevation",
                    Tooltip = "Goto without elevation\nPan Horizontal Azimuth only (Keep Current Tilt)",
                    IconKey = "goto_bearing",
                    AnimationType = IconAnimationType.CompassBearing,
                    AccentColor = Color.FromArgb(245, 158, 11), // Amber
                    OnClick = async (ctx) =>
                    {
                        var bearing = CalculateBearing(_pelco.Latitude, _pelco.Longitude, ctx.Latitude, ctx.Longitude);
                        double cameraAngle = (bearing + _pelco.AzimuthOffset + 360) % 360;

                        _pelco.GotoDeg(cameraAngle, _pelco.CurrentTilt, false);
                        lblStatus.Text = $"Action: Goto ({ctx.Latitude:F5}, {ctx.Longitude:F5}) Bearing only ({cameraAngle:F1}°)";
                        await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                    }
                });

                // 3. Add Target (Multi-Target)
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Add Target",
                    Tooltip = "Add Target\nDrop a new tactical target reticle at this position",
                    IconKey = "crosshair",
                    AnimationType = IconAnimationType.CrosshairLock,
                    AccentColor = Color.FromArgb(239, 68, 68), // Red
                    OnClick = async (ctx) =>
                    {
                        await AddTargetAt(ctx.Latitude, ctx.Longitude);
                    }
                });

                // 4. Set Camera Marker
                overlay.AddItem(new MapContextMenuItem
                {
                    Name = "Set Camera",
                    Tooltip = "Set Camera Position\nMove camera origin to this location",
                    IconKey = "camera_ptz",
                    AnimationType = IconAnimationType.Pulse,
                    AccentColor = Color.FromArgb(34, 197, 94), // Green
                    OnClick = async (ctx) =>
                    {
                        _pelco.Latitude = ctx.Latitude;
                        _pelco.Longitude = ctx.Longitude;
                        lblStatus.Text = $"Camera origin relocated to {ctx.Latitude:F5}, {ctx.Longitude:F5}";
                        await UpdateMainFormCameraDisplay(_pelco.Latitude, _pelco.Longitude, _pelco.CurrentPanNorthed, _pelco.CurrentTilt, _pelco.CurrentZoom, InstallHeight);
                    }
                });
            }
        }
    }
}
