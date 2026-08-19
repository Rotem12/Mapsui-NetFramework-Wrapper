using Mapsui48.Client;
using Mapsui48.Protocol;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mapsui48.Demo
{
    public partial class MainForm : Form
    {
        private MapHostPanel _mapPanel;
        private FloatingTooltipForm _tooltipForm;

        public MainForm()
        {
            InitializeComponent();
            _tooltipForm = new FloatingTooltipForm();
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
            
            // Hook live PointerMoved and PointerLeft events for the Custom Tooltip Test
            _mapPanel.PointerMoved += MapPanel_PointerMoved;
            _mapPanel.PointerLeft += (s, e) => { if (!IsDisposed) Invoke((MethodInvoker)delegate { _tooltipForm?.Hide(); }); };

            panelMapContainer.Controls.Add(_mapPanel);

            InitializeProviderDropdown();
            InitializeIconTestControls();
            InitializeContextMenuOverlay();
        }

        private void MapPanel_PointerMoved(object sender, MapPointerMovedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
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

        private void MapPanel_ViewportChanged(object sender, ViewportChangedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
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
            // Auto-calculate the bounding box of the loaded offline map and zoom to fit it perfectly
            await _mapPanel.GoHomeAsync(durationMs: 1500);
        }

        private async void btnFly_Click(object sender, EventArgs e)
        {
            // Fly to a specific target
            await _mapPanel.FlyToAsync(40.7128, -74.0060, zoom: 14, durationMs: 2000);
        }

        private async void btnAddPolygon_Click(object sender, EventArgs e)
        {
            // Add a sample polygon near the center
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
                await _mapPanel.AddPointAsync("Targets", 0.0, 0.0, label: "Center point", color: "#FF00FF00", scale: 1.5);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void InitializeContextMenuOverlay()
        {
            var overlay = _mapPanel.ContextMenuOverlay;
            overlay.ClearItems();

            // 1. Goto (3D Isometric Elevation)
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Goto",
                Tooltip = "Goto (With Elevation)\nCalculate Bearing & Pitch Angle",
                IconKey = "goto_elevation",
                AnimationType = IconAnimationType.Elevation3D,
                AccentColor = Color.FromArgb(0, 229, 255), // Glowing Cyan
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Goto ({ctx.Latitude:F5}, {ctx.Longitude:F5}) with Elevation calculation";
                    await _mapPanel.FlyToAsync(ctx.Latitude, ctx.Longitude, durationMs: 1000);
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Goto Target", color: "#00E5FF", scale: 1.4, iconType: "crosshair");
                }
            });

            // 2. Goto without elevation (Azimuth Bearing & Compass)
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Goto without elevation",
                Tooltip = "Goto without elevation\nPan Horizontal Azimuth Bearing only",
                IconKey = "goto_bearing",
                AnimationType = IconAnimationType.CompassBearing,
                AccentColor = Color.FromArgb(245, 158, 11), // Tactical Amber / Gold
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Goto ({ctx.Latitude:F5}, {ctx.Longitude:F5}) without Elevation (Azimuth Only)";
                    await _mapPanel.FlyToAsync(ctx.Latitude, ctx.Longitude, durationMs: 1000);
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Bearing Target", color: "#F59E0B", scale: 1.4, iconType: "arrow");
                }
            });

            // 3. Thermal IR Sensor
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Thermal IR",
                Tooltip = "Thermal IR Sensor\nActivate infrared raster scan",
                IconKey = "camera_thermal",
                AnimationType = IconAnimationType.ThermalScan,
                AccentColor = Color.FromArgb(245, 158, 11), // Amber / Red
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Thermal Scan at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Thermal FLIR", color: "#F59E0B", scale: 1.3, iconType: "camera_thermal");
                }
            });

            // 4. Laser Rangefinder
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Laser LRF",
                Tooltip = "Laser Rangefinder\nPulsing precision range telemetry",
                IconKey = "laser",
                AnimationType = IconAnimationType.LaserRangefinder,
                AccentColor = Color.FromArgb(239, 68, 68), // Ruby Red
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: LRF Measurement at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "LRF Mark", color: "#EF4444", scale: 1.3, iconType: "crosshair");
                }
            });

            // 5. Missile Trajectory / Strike
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Missile Strike",
                Tooltip = "Missile Trajectory\nBallistic flight path & impact zone",
                IconKey = "missile",
                AnimationType = IconAnimationType.MissileTrajectory,
                AccentColor = Color.FromArgb(239, 68, 68), // Red
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Missile Target at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Strike Target", color: "#EF4444", scale: 1.3, iconType: "missile");
                }
            });

            // 6. Orbit Satellite Downlink
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Orbit Sat",
                Tooltip = "Satellite Uplink\nSatcom conical radio transmission",
                IconKey = "satellite",
                AnimationType = IconAnimationType.OrbitSatellite,
                AccentColor = Color.FromArgb(0, 229, 255), // Cyan
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Satellite Uplink at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "SATCOM Link", color: "#00E5FF", scale: 1.3, iconType: "satellite");
                }
            });

            // 7. Tactical Shield Defense
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Shield Def",
                Tooltip = "Energy Shield\nHexagonal barrier defense ripple",
                IconKey = "shield",
                AnimationType = IconAnimationType.ShieldDefense,
                AccentColor = Color.FromArgb(34, 197, 94), // Green
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Defense Shield deployed at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddCircleAsync("Zones", ctx.Latitude, ctx.Longitude, 600, fillColor: "#2522C55E", outlineColor: "#22C55E", outlineWidth: 2.0);
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Shield Base", color: "#22C55E", scale: 1.3, iconType: "shield");
                }
            });

            // 8. Tactical Radar Scan
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Radar Scan",
                Tooltip = "Radar Scan\nInitiate 360° tactical sweep at position",
                IconKey = "radar",
                AnimationType = IconAnimationType.RadarSweep,
                AccentColor = Color.FromArgb(34, 197, 94), // Green
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Radar Sweep at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddCircleAsync("Markers", ctx.Latitude, ctx.Longitude, 500, fillColor: "#2022C55E", outlineColor: "#22C55E", outlineWidth: 2.0);
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "Radar Station", color: "#22C55E", scale: 1.3, iconType: "radar");
                }
            });

            // 9. Sniper Optical Scope
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Sniper Scope",
                Tooltip = "Sniper Optic\nMil-dot optical magnification zoom",
                IconKey = "sniper",
                AnimationType = IconAnimationType.SniperScope,
                AccentColor = Color.FromArgb(239, 68, 68), // Red
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Sniper Overwatch at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Targets", ctx.Latitude, ctx.Longitude, label: "Sniper Pos", color: "#EF4444", scale: 1.3, iconType: "sniper");
                }
            });

            // 10. Helipad LZ
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Helipad LZ",
                Tooltip = "Landing Zone\nRotor blade spin & flashing beacons",
                IconKey = "helipad",
                AnimationType = IconAnimationType.HelipadLZ,
                AccentColor = Color.FromArgb(245, 158, 11), // Amber
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Helipad LZ at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "LZ Bravo", color: "#F59E0B", scale: 1.3, iconType: "helipad");
                }
            });

            // 11. Tactical Unit / Drone
            overlay.AddItem(new MapContextMenuItem
            {
                Name = "Deploy Drone",
                Tooltip = "Deploy UAV Drone\nSpawn airborne surveillance asset",
                IconKey = "drone",
                AnimationType = IconAnimationType.Pulse,
                AccentColor = Color.FromArgb(168, 85, 247), // Purple
                OnClick = async (ctx) =>
                {
                    lblStatus.Text = $"Action: Deployed UAV Drone at ({ctx.Latitude:F5}, {ctx.Longitude:F5})";
                    await _mapPanel.AddPointAsync("Markers", ctx.Latitude, ctx.Longitude, label: "UAV-1", color: "#A855F7", scale: 1.3, iconType: "drone");
                }
            });
        }
    }

    /// <summary>
    /// Lightweight non-activating floating tooltip window that renders above all Win32 and OpenGL child surfaces.
    /// </summary>
    internal class FloatingTooltipForm : Form
    {
        private readonly Label _lblText;

        public FloatingTooltipForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(20, 20, 20);
            TopMost = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(1);

            _lblText = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(0, 229, 255),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0)
            };
            Controls.Add(_lblText);
        }

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

        public void UpdateTooltip(string text, Point screenPoint)
        {
            _lblText.Text = text;
            Location = new Point(screenPoint.X + 16, screenPoint.Y + 16);
            if (!Visible)
            {
                Show();
            }
        }
    }
}
