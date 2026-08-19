using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Mapsui48.Client;
using Mapsui48.Protocol;

namespace PelcoControlNM
{
    public partial class ctlMap : UserControl
    {
        private PelcoControl parent;

        public PelcoControl Pelco
        {
            get => parent;
            set
            {
                parent = value;
                if (loaded)
                {
                    _ = SetPosition(false);
                }
            }
        }

        public double ddLat1 { get; set; }
        public double ddLon1 { get; set; }
        public double ddLat2 { get; set; }
        public double ddLon2 { get; set; }

        public double ddLatPos { get; set; }
        public double ddLonPos { get; set; }

        public double CamElevation { get; set; }

        public bool HasGPS { get; set; }
        public bool HasGPS2 { get; set; }

        // Target tracking model so Highlight/Dehighlight only need 'name'
        private class TargetInfo
        {
            public string FeatureId { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public bool IsHighlighted { get; set; }
        }

        private Dictionary<string, TargetInfo> Targets = new Dictionary<string, TargetInfo>();
        private string _cameraMarkerId;

        // Custom Icon & Coloring Configuration
        public string CameraIconType { get; set; } = "camera_ptz";
        public string TargetIconType { get; set; } = "crosshair";
        public string CameraColor { get; set; } = "#00D4FF";
        public string TargetColor { get; set; } = "#EF4444";
        public string HighlightColor { get; set; } = "#00E5FF";

        // Viewport tracking
        private double _currentCenterLat = 31.5;
        private double _currentCenterLon = 34.75;
        private double _currentZoom = 12;

        private (double Lat, double Lon) lastPoint;
        private double lastElevation;

        bool loaded = false;

        ElevationManager elevationManager;

        public int InstallHeight { get; set; }
        public int MaxRange { get; set; } = 50;
        public double HRes { get; set; } = 1920;
        public double VRes { get; set; } = 1080;

        MacroTooltipController tooltip = new MacroTooltipController();
        private FloatingTooltipForm _floatingTooltip;
        private CancellationTokenSource cts;
        private DateTime startTime;

        private const double R = 6371000; // Earth radius in meters

        private AreaSelectedEvent _selectedArea;
        private Form _parentForm;

        public MapHostPanel MapHostPanel => map;
        public MapHostPanel MapControl => map;

        public ctlMap() : this(null)
        {
        }

        public ctlMap(PelcoControl pelco)
        {
            InitializeComponent();
            parent = pelco;
            _floatingTooltip = new FloatingTooltipForm();
            initMap();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            HookParentForm();
        }

        private void HookParentForm()
        {
            if (_parentForm != null)
            {
                _parentForm.FormClosing -= ParentForm_FormClosing;
                _parentForm.Deactivate -= ParentForm_Deactivate;
                _parentForm = null;
            }

            var topForm = FindForm();
            if (topForm != null)
            {
                _parentForm = topForm;
                _parentForm.FormClosing += ParentForm_FormClosing;
                _parentForm.Deactivate += ParentForm_Deactivate;
            }
        }

        private void ParentForm_Deactivate(object sender, EventArgs e)
        {
            _floatingTooltip?.Hide();
        }

        private void ParentForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                try { cts.Cancel(); } catch { }
            }

            SaveSettings();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                _floatingTooltip?.Hide();
            }
        }

        private void initMap()
        {
            string mapFile = @"C:\maps\israel.mbtiles";
            string cacheFolder = @"C:\MapCache";

            // Configure MapHostPanel properties
            map.OnlineUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
            if (File.Exists(mapFile))
            {
                map.MBTilesPath = mapFile;
            }
            map.CachePath = cacheFolder;

            // Hide the downloader panel by default until an area is selected
            if (panel1 != null)
            {
                panel1.Visible = false;
            }

            // 1. Initialize Interactive Right-Click Context Menu Action Panel
            InitializeActionContextMenu();

            if (this.contextMenuStrip1 != null)
            {
                map.ContextMenuStrip = this.contextMenuStrip1;
            }

            // 2. Configure the top on-screen crosshair button to smoothly center on the camera position
            map.CustomHomeAction = async () =>
            {
                await Center();
            };

            // Subscribe to Mapsui events
            map.PointerMoved += MapPanel_PointerMoved;
            map.PointerLeft += (s, e) => { if (!IsDisposed) BeginInvoke((MethodInvoker)delegate { _floatingTooltip?.Hide(); }); };
            map.AreaSelected += MapPanel_AreaSelected;
            map.MapClicked += MapPanel_MapClicked;
            map.FeatureClicked += MapPanel_FeatureClicked;
            map.ViewportChanged += MapPanel_ViewportChanged;

            // Initialize dropdown providers
            InitializeProviderDropdown();

            // Initialize Elevation Manager
            elevationManager = new ElevationManager(@"C:\MapCache\Elevations", "cca51971858ebe853218ee20d8b78191", ElevationMode.CacheOnly);

            LoadSettings();
            loaded = true;
        }

        private class ProviderItem
        {
            public string Name { get; set; }
            public string Url { get; set; }
        }

        private void InitializeProviderDropdown()
        {
            var cmbProvider = comboBoxMapProvider;
            if (cmbProvider == null) return;

            cmbProvider.Items.Clear();

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
                    await map.ChangeOnlineProviderAsync(selected.Url);
                }
            };
        }

        // Live Mouse Hover Event -> Displays the Floating Tooltip
        private void MapPanel_PointerMoved(object sender, MapPointerMovedEvent e)
        {
            if (IsDisposed) return;
            double elev = elevationManager != null ? elevationManager.GetElevation(e.Latitude, e.Longitude) : 0;
            var screenPoint = Cursor.Position;
            _floatingTooltip?.UpdateTooltip($"Lat: {e.Latitude:F4}\nLon: {e.Longitude:F4}\nAlt: {elev:F1}m", screenPoint);

            try
            {
                tooltip?.SetText(map, $"Lat: {e.Latitude:F4}\nLon: {e.Longitude:F4}\nAlt: {elev:F1}m");
            }
            catch { }
        }

        // Area Selection Event -> Shows the Downloader Panel (panel1)
        private void MapPanel_AreaSelected(object sender, AreaSelectedEvent e)
        {
            _selectedArea = e;
            if (IsDisposed) return;
            if (panel1 != null)
            {
                panel1.Visible = true;
                panel1.BringToFront();
            }
        }

        private void MapPanel_ViewportChanged(object sender, ViewportChangedEvent e)
        {
            _currentCenterLat = e.CenterLat;
            _currentCenterLon = e.CenterLon;
            _currentZoom = e.ZoomLevel;
        }

        private void MapPanel_FeatureClicked(object sender, FeatureClickedEvent e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                MessageBox.Show($"Feature: {e.FeatureId} (Layer: {e.LayerName}) at {e.Latitude:F4}, {e.Longitude:F4}", "Feature Selected");
            });
        }

        private void MapPanel_MapClicked(object sender, MapClickedEvent e)
        {
            lastPoint = (e.Latitude, e.Longitude);
            lastElevation = elevationManager != null ? elevationManager.GetElevation(lastPoint.Lat, lastPoint.Lon) : 0;

            // Left-click clears active area selection and hides panel1
            if (e.Button == "Left" && _selectedArea != null)
            {
                _selectedArea = null;
                _ = map.ClearLayerAsync("Selection");
                if (!IsHandleCreated || IsDisposed) return;
                Invoke((MethodInvoker)delegate
                {
                    if (panel1 != null)
                        panel1.Visible = false;
                });
            }
        }

        public async void LoadSettings()
        {
            try
            {
                XmlLoader xml = new XmlLoader(PelcoControl.DirectoryPath + PelcoControl.XMLName, true);

                bool showDefault = false;
                string pos = xml.Get("Map/Position", "");
                if (!string.IsNullOrEmpty(pos))
                {
                    string[] p = pos.Split(',');
                    if (p.Length >= 2 && double.TryParse(p[0], out double parsedLat) && double.TryParse(p[1], out double parsedLon) && (Math.Abs(parsedLat) > 0.001 || Math.Abs(parsedLon) > 0.001))
                    {
                        ddLatPos = parsedLat;
                        ddLonPos = parsedLon;
                    }
                    else
                    {
                        showDefault = true;
                        ddLatPos = (parent != null && Math.Abs(parent.Latitude) > 0.001) ? parent.Latitude : 31.5;
                        ddLonPos = (parent != null && Math.Abs(parent.Longitude) > 0.001) ? parent.Longitude : 34.75;
                    }
                }
                else
                {
                    showDefault = true;
                    ddLatPos = (parent != null && Math.Abs(parent.Latitude) > 0.001) ? parent.Latitude : 31.5;
                    ddLonPos = (parent != null && Math.Abs(parent.Longitude) > 0.001) ? parent.Longitude : 34.75;
                }

                double zoom = xml.Get("Map/Zoom", 0d);
                if (zoom <= 0)
                {
                    zoom = GetZoomForRadiusKm(showDefault ? 235 : 5, ddLatPos);
                }

                zoom = MathE.Clamp(zoom, 2, 18);
                _currentZoom = zoom;

                // Atomically set both position and zoom level directly upon startup
                CameraIconType = xml.Get("Map/CameraIconType", "camera_ptz");
                TargetIconType = xml.Get("Map/TargetIconType", "crosshair");
                CameraColor = xml.Get("Map/CameraColor", "#00D4FF");
                TargetColor = xml.Get("Map/TargetColor", "#EF4444");
                HighlightColor = xml.Get("Map/HighlightColor", "#00E5FF");

                InitializeIconContextMenu();

                await map.NavigateToAsync(ddLatPos, ddLonPos, zoom);
                await SetPosition(center: false);
            }
            catch
            {
                // Fallback for standalone / designer environments
                ddLatPos = (parent != null && Math.Abs(parent.Latitude) > 0.001) ? parent.Latitude : 31.5;
                ddLonPos = (parent != null && Math.Abs(parent.Longitude) > 0.001) ? parent.Longitude : 34.75;
                _currentZoom = 12;

                InitializeIconContextMenu();

                await map.NavigateToAsync(ddLatPos, ddLonPos, _currentZoom);
                await SetPosition(center: false);
            }
        }

        public void SaveSettings()
        {
            try
            {
                XmlLoader xml = new XmlLoader(PelcoControl.DirectoryPath + PelcoControl.XMLName);

                if (Math.Abs(_currentCenterLat) > 0.001 || Math.Abs(_currentCenterLon) > 0.001)
                {
                    xml.Set("Map/Position", $"{_currentCenterLat},{_currentCenterLon}");
                }
                if (_currentZoom > 1)
                {
                    xml.Set("Map/Zoom", _currentZoom);
                }

                xml.Set("Map/CameraIconType", CameraIconType);
                xml.Set("Map/TargetIconType", TargetIconType);
                xml.Set("Map/CameraColor", CameraColor);
                xml.Set("Map/TargetColor", TargetColor);
                xml.Set("Map/HighlightColor", HighlightColor);

                xml.Save();
            }
            catch { }
        }

        private void InitializeIconContextMenu()
        {
            if (this.contextMenuStrip1 == null)
            {
                this.contextMenuStrip1 = new ContextMenuStrip();
            }

            var menu = this.contextMenuStrip1;

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
            }

            // 1. Target Icon Submenu
            var miTargetIcon = new ToolStripMenuItem("Target Icon");
            foreach (var group in MapIconCatalog.GetIconsByCategory())
            {
                var grpItem = new ToolStripMenuItem(group.Key);
                foreach (var icon in group)
                {
                    var item = new ToolStripMenuItem(icon.Name);
                    item.Tag = icon.Key;
                    item.Click += async (s, e) =>
                    {
                        TargetIconType = icon.Key;
                        SaveSettings();
                        await RefreshAllTargets();
                    };
                    grpItem.DropDownItems.Add(item);
                }
                miTargetIcon.DropDownItems.Add(grpItem);
            }
            menu.Items.Add(miTargetIcon);

            // 2. Target Color Submenu
            var miTargetColor = new ToolStripMenuItem("Target Color");
            foreach (var color in MapIconCatalog.StandardColors)
            {
                var item = new ToolStripMenuItem(color.Name);
                item.Tag = color.Hex;
                item.Click += async (s, e) =>
                {
                    TargetColor = color.Hex;
                    SaveSettings();
                    await RefreshAllTargets();
                };
                miTargetColor.DropDownItems.Add(item);
            }
            var miCustomTargetColor = new ToolStripMenuItem("Custom Color...");
            miCustomTargetColor.Click += async (s, e) =>
            {
                using (var cd = new ColorDialog())
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        TargetColor = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                        SaveSettings();
                        await RefreshAllTargets();
                    }
                }
            };
            miTargetColor.DropDownItems.Add(miCustomTargetColor);
            menu.Items.Add(miTargetColor);

            // 3. Camera Icon Submenu
            var miCameraIcon = new ToolStripMenuItem("Camera Icon");
            foreach (var icon in MapIconCatalog.GetAllIcons().Where(i => i.Category.Contains("Camera") || i.Category.Contains("Sensor")))
            {
                var item = new ToolStripMenuItem(icon.Name);
                item.Tag = icon.Key;
                item.Click += async (s, e) =>
                {
                    CameraIconType = icon.Key;
                    SaveSettings();
                    await SetPosition(false);
                };
                miCameraIcon.DropDownItems.Add(item);
            }
            menu.Items.Add(miCameraIcon);

            // 4. Camera Color Submenu
            var miCameraColor = new ToolStripMenuItem("Camera Color");
            foreach (var color in MapIconCatalog.StandardColors)
            {
                var item = new ToolStripMenuItem(color.Name);
                item.Tag = color.Hex;
                item.Click += async (s, e) =>
                {
                    CameraColor = color.Hex;
                    SaveSettings();
                    await SetPosition(false);
                };
                miCameraColor.DropDownItems.Add(item);
            }
            var miCustomCameraColor = new ToolStripMenuItem("Custom Color...");
            miCustomCameraColor.Click += async (s, e) =>
            {
                using (var cd = new ColorDialog())
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        CameraColor = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                        SaveSettings();
                        await SetPosition(false);
                    }
                }
            };
            miCameraColor.DropDownItems.Add(miCustomCameraColor);
            menu.Items.Add(miCameraColor);

            map.ContextMenuStrip = menu;
        }

        private void InitializeActionContextMenu()
        {
            if (map?.ContextMenuOverlay == null) return;

            map.ContextMenuOverlay.ClearItems();

            // 1. Goto (With Elevation) -> 3D Isometric Pitch & Altitude Trajectory Animation
            map.ContextMenuOverlay.AddItem(new MapContextMenuItem
            {
                Name = "Goto",
                Tooltip = "Goto (With Elevation)\nCalculate Bearing & Pitch Angle using Elevation Data",
                IconKey = "goto_elevation",
                AnimationType = IconAnimationType.Elevation3D,
                AccentColor = Color.FromArgb(0, 229, 255), // Glowing Cyan
                OnClick = (ctx) =>
                {
                    lastPoint = (ctx.Latitude, ctx.Longitude);
                    lastElevation = elevationManager != null ? elevationManager.GetElevation(lastPoint.Lat, lastPoint.Lon) : 0;
                    double camAlt = (elevationManager != null ? elevationManager.GetElevation(ddLatPos, ddLonPos) : 0) + InstallHeight;
                    var angles = GetAngles(ddLatPos, ddLonPos, camAlt, lastPoint.Lat, lastPoint.Lon, lastElevation);

                    parent?.GotoDeg(angles.Bearing + (parent != null ? parent.AzimuthOffset : 0), angles.Pitch + (parent != null ? parent.ElevationOffset : 0), false);
                }
            });

            // 2. Goto without elevation -> 2D Tactical Azimuth Compass & Radar Sweep Animation
            map.ContextMenuOverlay.AddItem(new MapContextMenuItem
            {
                Name = "Goto without elevation",
                Tooltip = "Goto without elevation\nPan Horizontal Azimuth only (Keep Current Tilt)",
                IconKey = "goto_bearing",
                AnimationType = IconAnimationType.CompassBearing,
                AccentColor = Color.FromArgb(245, 158, 11), // Tactical Amber / Gold
                OnClick = (ctx) =>
                {
                    lastPoint = (ctx.Latitude, ctx.Longitude);
                    var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
                    double cameraAngle = (bearing + (parent != null ? parent.AzimuthOffset : 0) + 360) % 360;

                    parent?.GotoDeg(cameraAngle, parent != null ? parent.CurrentTilt : 0, false);
                }
            });

            // 3. Set Target Marker Quick-Action
            map.ContextMenuOverlay.AddItem(new MapContextMenuItem
            {
                Name = "Set Target",
                Tooltip = "Set as Main Target\nDrop tactical target reticle at this position",
                IconKey = "crosshair",
                AnimationType = IconAnimationType.CrosshairLock,
                AccentColor = Color.FromArgb(239, 68, 68), // Tactical Red
                OnClick = async (ctx) =>
                {
                    await ShowTarget(new double[] { ctx.Latitude, ctx.Longitude });
                }
            });

            // 4. Set Camera Marker Quick-Action
            map.ContextMenuOverlay.AddItem(new MapContextMenuItem
            {
                Name = "Set Camera",
                Tooltip = "Set Camera Position\nMove camera origin to this location",
                IconKey = "camera_ptz",
                AnimationType = IconAnimationType.Pulse,
                AccentColor = Color.FromArgb(34, 197, 94), // Tactical Green
                OnClick = async (ctx) =>
                {
                    ddLatPos = ctx.Latitude;
                    ddLonPos = ctx.Longitude;
                    SaveSettings();
                    await SetPosition(center: false);
                }
            });
        }

        private async Task RefreshAllTargets()
        {
            foreach (var kvp in Targets.ToList())
            {
                var t = kvp.Value;
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await map.RemoveFeatureAsync("Targets", t.FeatureId);
                }
                string col = t.IsHighlighted ? HighlightColor : TargetColor;
                double sc = t.IsHighlighted ? 1.5 : 1.2;
                t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: kvp.Key, color: col, scale: sc, iconType: TargetIconType);
            }
        }

        // ==========================================
        // Camera Positioning
        // ==========================================

        public async Task SetPosition(bool center = false)
        {
            await map.ClearLayerAsync("Camera");
            double rotation = parent != null ? parent.CurrentPanNorthed : 0;
            _cameraMarkerId = await map.AddPointAsync("Camera", ddLatPos, ddLonPos, label: "Camera", color: CameraColor, scale: 1.0, rotation: rotation, iconType: CameraIconType);

            if (center)
            {
                await Center();
            }
        }

        public void SetPositionSync(bool center = false)
        {
            _ = SetPosition(center);
        }

        public async Task Center()
        {
            await map.NavigateToAsync(ddLatPos, ddLonPos, _currentZoom);
        }

        // ==========================================
        // Target Tracking Management
        // ==========================================

        public async Task ShowTarget(double[] target)
        {
            if (target == null || target.Length < 2) return;

            await ClearTarget();
            string id = await map.AddPointAsync("Targets", target[0], target[1], label: "Target", color: TargetColor, scale: 1.5, iconType: TargetIconType);
            Targets["MainTarget"] = new TargetInfo { FeatureId = id, Lat = target[0], Lon = target[1] };

            EnsurePointIsVisible(target[0], target[1]);
        }

        public async Task AddTarget(string name, double[] target, string iconType = null, string color = null)
        {
            if (target == null || target.Length < 2) return;

            if (Targets.TryGetValue(name, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.FeatureId))
                    await map.RemoveFeatureAsync("Targets", existing.FeatureId);
            }

            string id = await map.AddPointAsync("Targets", target[0], target[1], label: name, color: color ?? TargetColor, scale: 1.2, iconType: iconType ?? TargetIconType);
            Targets[name] = new TargetInfo { FeatureId = id, Lat = target[0], Lon = target[1], IsHighlighted = false };
        }

        public async Task HighlightTarget(string name)
        {
            if (Targets.TryGetValue(name, out var t))
            {
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await map.RemoveFeatureAsync("Targets", t.FeatureId);
                }

                t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: name, color: HighlightColor, scale: 1.6, iconType: TargetIconType);
                t.IsHighlighted = true;

                EnsurePointIsVisible(t.Lat, t.Lon);
            }
        }

        public async Task DehighlightTarget(string name)
        {
            if (Targets.TryGetValue(name, out var t))
            {
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await map.RemoveFeatureAsync("Targets", t.FeatureId);
                }

                t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: name, color: TargetColor, scale: 1.2, iconType: TargetIconType);
                t.IsHighlighted = false;
            }
        }

        public async Task ClearTarget()
        {
            Targets.Clear();
            await map.ClearLayerAsync("Targets");
        }

        // ==========================================
        // Vision FOV Triangle
        // ==========================================

        public async Task Rotate()
        {
            double camAlt = (elevationManager != null ? elevationManager.GetElevation(ddLatPos, ddLonPos) : 0) + InstallHeight;
            double camPan = parent != null ? parent.CurrentPanNorthed : 0;

            // 1. Update Camera Marker with active pan rotation
            await map.ClearLayerAsync("Camera");
            _cameraMarkerId = await map.AddPointAsync("Camera", ddLatPos, ddLonPos, label: "Camera", color: CameraColor, scale: 1.0, rotation: camPan, iconType: CameraIconType);

            // 2. Update Vision FOV Cone
            double[][] visionCoords = CreateVisionTriangle(
                ddLatPos, 
                ddLonPos, 
                camAlt, 
                camPan, 
                parent != null ? parent.CurrentTilt : 0, 
                parent != null ? parent.CurrentZoom : 30, 
                stepMeters: 30, 
                maxRangeMeters: MaxRange * 1000
            );

            await map.ClearLayerAsync("Vision");
            if (visionCoords != null && visionCoords.Length >= 3)
            {
                await map.AddPolygonAsync("Vision", visionCoords, fillColor: "#3C8B0000", outlineColor: "#8B0000", outlineWidth: 1.5);
            }
        }

        private async void EnsurePointIsVisible(double lat, double lon)
        {
            double distKm = DistanceKm(_currentCenterLat, _currentCenterLon, lat, lon);
            double requiredZoom = GetZoomForRadiusKm(Math.Max(distKm, 2), _currentCenterLat);
            requiredZoom = MathE.Clamp(requiredZoom, 2, 18);

            if (distKm > 5 || _currentZoom > requiredZoom)
            {
                await map.FlyToAsync(lat, lon, requiredZoom, 800);
            }
        }

        private async void btnCenter_Click(object sender, EventArgs e)
        {
            await Center();
        }

        double GetZoomForRadiusKm(double radiusKm, double latitude)
        {
            const double EarthCircumference = 40075016.686;
            double latitudeRad = latitude * Math.PI / 180.0;
            double metersPerPixel = (radiusKm * 1000 * 2) / Math.Min(map.Width > 0 ? map.Width : 500, map.Height > 0 ? map.Height : 350);
            double zoom = Math.Log(EarthCircumference * Math.Cos(latitudeRad) / (metersPerPixel * 256), 2);
            return zoom;
        }

        double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double Rkm = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;

            double p1 = lat1 * Math.PI / 180.0;
            double p2 = lat2 * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(p1) * Math.Cos(p2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Rkm * c;
        }

        private void comboBoxMapProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        // ==========================================
        // Offline Map & Elevation Downloader
        // ==========================================

        private async void buttonDownload_Click(object sender, EventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
                progressBarDownload.Value = 0;
                labelETA.Text = "ETA: Cancelled";
                return;
            }

            cts = new CancellationTokenSource();
            labelETA.Text = "ETA: Starting download...";
            progressBarDownload.Value = 0;
            startTime = DateTime.Now;

            var bbox = new BoundingBox
            {
                MinLat = _selectedArea != null ? _selectedArea.MinLat : _currentCenterLat - 0.15,
                MaxLat = _selectedArea != null ? _selectedArea.MaxLat : _currentCenterLat + 0.15,
                MinLon = _selectedArea != null ? _selectedArea.MinLon : _currentCenterLon - 0.20,
                MaxLon = _selectedArea != null ? _selectedArea.MaxLon : _currentCenterLon + 0.20
            };

            int minZoom = (int)Math.Max(2, Math.Floor(_currentZoom));
            int maxZoom = (int)Math.Min(16, minZoom + 3);

            string outputMbtiles = Path.Combine(map.CachePath ?? @"C:\MapCache", $"Area_{DateTime.Now:yyyyMMdd_HHmmss}.mbtiles");

            try
            {
                var progress = new Progress<int>(downloaded =>
                {
                    int total = TileDownloaderEngine.CalculateTotalTiles(bbox, minZoom, maxZoom);
                    double perc = total > 0 ? (downloaded * 100.0 / total) : 0;
                    downloadProgress(perc, downloaded, total);
                });

                // 1. Cache Elevation Tiles
                await Task.Run(() =>
                {
                    elevationManager.CacheArea(bbox.MinLat, bbox.MaxLat, bbox.MinLon, bbox.MaxLon);
                }, cts.Token);

                // 2. Download Map Tiles to MBTiles
                await TileDownloaderEngine.DownloadRegionAsync(
                    outputMbtiles,
                    bbox,
                    minZoom,
                    maxZoom,
                    map.OnlineUrl,
                    progress,
                    cts.Token
                );

                labelETA.Text = "ETA: Complete!";
                progressBarDownload.Value = 100;
            }
            catch (OperationCanceledException)
            {
                labelETA.Text = "ETA: Cancelled";
                progressBarDownload.Value = 0;
            }
            catch (Exception ex)
            {
                labelETA.Text = "Error: " + ex.Message;
            }
            finally
            {
                cts = null;
            }
        }

        private void downloadProgress(double perc, int downloaded, int total)
        {
            if (downloaded <= 0) return;

            var elapsed = DateTime.Now - startTime;
            double msPerTile = elapsed.TotalMilliseconds / downloaded;
            long tilesRemaining = Math.Max(0, total - downloaded);
            double remainingMs = tilesRemaining * msPerTile;
            TimeSpan eta = TimeSpan.FromMilliseconds(remainingMs);

            labelETA.Text = $"ETA: {eta.Minutes:D2}:{eta.Seconds:D2} ({downloaded}/{total})";
            progressBarDownload.Value = (int)Math.Min(100, Math.Max(0, perc));
        }

        private void miGoto_Click(object sender, EventArgs e)
        {
            double camAlt = (elevationManager != null ? elevationManager.GetElevation(ddLatPos, ddLonPos) : 0) + InstallHeight;
            var angles = GetAngles(ddLatPos, ddLonPos, camAlt, lastPoint.Lat, lastPoint.Lon, lastElevation);

            parent?.GotoDeg(angles.Bearing + (parent != null ? parent.AzimuthOffset : 0), angles.Pitch + (parent != null ? parent.ElevationOffset : 0), false);
        }

        private void miGotoNoElevation_Click(object sender, EventArgs e)
        {
            var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
            double cameraAngle = (bearing + (parent != null ? parent.AzimuthOffset : 0) + 360) % 360;

            parent?.GotoDeg(cameraAngle, parent != null ? parent.CurrentTilt : 0, false);
        }

        public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double phi1 = lat1 * Math.PI / 180.0;
            double phi2 = lat2 * Math.PI / 180.0;
            double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;

            double y = Math.Sin(deltaLambda) * Math.Cos(phi2);
            double x = Math.Cos(phi1) * Math.Sin(phi2) -
                       Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);

            double bearing = Math.Atan2(y, x);
            bearing = bearing * 180.0 / Math.PI;

            return (bearing + 360) % 360;
        }

        public static (double Bearing, double Pitch) GetAngles(double lat1, double lon1, double alt1,
                                                               double lat2, double lon2, double alt2)
        {
            const double R = 6371000;

            double p1 = lat1 * Math.PI / 180.0;
            double p2 = lat2 * Math.PI / 180.0;
            double dL = (lon2 - lon1) * Math.PI / 180.0;
            double dP = (lat2 - lat1) * Math.PI / 180.0;

            double y = Math.Sin(dL) * Math.Cos(p2);
            double x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dL);
            double bearing = (Math.Atan2(y, x) * 180.0 / Math.PI + 360) % 360;

            double a = Math.Sin(dP / 2) * Math.Sin(dP / 2) +
                       Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dL / 2) * Math.Sin(dL / 2);
            double horizontalDist = R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double pitch = Math.Atan2(alt2 - alt1, horizontalDist) * 180.0 / Math.PI;

            return (bearing, pitch);
        }

        public ((double Lat, double Lon) Start, (double Lat, double Lon) Stop) FindGroundIntersection(
            (double Lat, double Lon) start, 
            double startAlt, 
            double bearing, 
            double pitch, 
            double vfov, 
            double stepMeters, 
            double maxRangeMeters)
        {
            double pitchRad = pitch * Math.PI / 180.0;
            double bearingRad = bearing * Math.PI / 180.0;
            double lat1Rad = start.Lat * Math.PI / 180.0;
            double lon1Rad = start.Lon * Math.PI / 180.0;
            double vFovRad = vfov * Math.PI / 180.0;

            (double Lat, double Lon) startIntersection = (0, 0);
            bool foundStart = false;

            for (double d = stepMeters; d < maxRangeMeters; d += stepMeters)
            {
                double currLatRad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(d / R) +
                                    Math.Cos(lat1Rad) * Math.Sin(d / R) * Math.Cos(bearingRad));
                double currLonRad = lon1Rad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(d / R) * Math.Cos(lat1Rad),
                                    Math.Cos(d / R) - Math.Sin(lat1Rad) * Math.Sin(currLatRad));

                double currLat = currLatRad * 180.0 / Math.PI;
                double currLon = currLonRad * 180.0 / Math.PI;

                double visionTopAlt = startAlt + (d * (Math.Tan(pitchRad) + Math.Tan(vFovRad / 2)));
                double visionBotAlt = startAlt + (d * (Math.Tan(pitchRad) - Math.Tan(vFovRad / 2)));

                double groundAlt = (elevationManager != null) ? elevationManager.GetElevation(currLat, currLon) : 0;

                if (!foundStart)
                {
                    if (visionBotAlt <= groundAlt)
                    {
                        foundStart = true;
                        startIntersection = (currLat, currLon);
                    }
                }
                else if (visionTopAlt <= groundAlt)
                {
                    return (startIntersection, (currLat, currLon));
                }
            }

            return (startIntersection, GetPointAtDistance(start, bearing, maxRangeMeters));
        }

        public double[][] CreateVisionTriangle(
            double originLat, 
            double originLon, 
            double alt, 
            double bearing, 
            double pitch, 
            double fov = 30, 
            double stepMeters = 20, 
            double maxRangeMeters = 50000)
        {
            var points = new List<(double Lat, double Lon)>();
            double vFov = (HRes > 0 && VRes > 0) ? fov * (VRes / HRes) : fov * (9.0 / 16.0);

            for (double a = bearing - (fov / 2); a < bearing + (fov / 2); a += fov * 0.1d)
            {
                var hitPoint = FindGroundIntersection((originLat, originLon), alt, a, pitch, vFov, stepMeters, maxRangeMeters);
                if (hitPoint.Start.Lat != 0 || hitPoint.Start.Lon != 0)
                {
                    points.Insert(0, hitPoint.Start);
                }
                points.Add(hitPoint.Stop);
            }

            if (points.Count < 3) return null;

            return points.Select(p => new double[] { p.Lat, p.Lon }).ToArray();
        }

        private (double Lat, double Lon) GetPointAtDistance((double Lat, double Lon) start, double brngDeg, double dist)
        {
            double brng = brngDeg * Math.PI / 180.0;
            double lat1 = start.Lat * Math.PI / 180.0;
            double lon1 = start.Lon * Math.PI / 180.0;

            double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(dist / R) + Math.Cos(lat1) * Math.Sin(dist / R) * Math.Cos(brng));
            double lon2 = lon1 + Math.Atan2(Math.Sin(brng) * Math.Sin(dist / R) * Math.Cos(lat1), Math.Cos(dist / R) - Math.Sin(lat1) * Math.Sin(lat2));

            return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
        }
    }
}
