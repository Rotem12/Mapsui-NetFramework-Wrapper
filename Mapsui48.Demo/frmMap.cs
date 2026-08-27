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
    public partial class frmMap : Form
    {
        PelcoControl parent;

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

        public bool EnableOfflineMap
        {
            get => map.EnableOfflineMap;
            set => map.EnableOfflineMap = value;
        }

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

        public bool FollowCamera { get; set; } = false;

        public frmMap(PelcoControl pelco)
        {
            InitializeComponent();
            parent = pelco;
            if (parent != null)
            {
                if (Math.Abs(parent.Latitude) > 0.001 || Math.Abs(parent.Longitude) > 0.001)
                {
                    ddLatPos = parent.Latitude;
                    ddLonPos = parent.Longitude;
                }
                parent.PositionChanged += async (s, e) =>
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        ddLatPos = parent.Latitude;
                        ddLonPos = parent.Longitude;
                        await Rotate();
                        if (FollowCamera)
                        {
                            await map.NavigateToAsync(ddLatPos, ddLonPos, _currentZoom);
                        }
                    }
                };
                parent.PanChanged += async (s, e) =>
                {
                    if (!IsDisposed && IsHandleCreated) await Rotate();
                };
                parent.TiltChanged += async (s, e) =>
                {
                    if (!IsDisposed && IsHandleCreated) await Rotate();
                };
                parent.ZoomChanged += async (s, e) =>
                {
                    if (!IsDisposed && IsHandleCreated) await Rotate();
                };
            }
            _floatingTooltip = new FloatingTooltipForm();
            initMap();
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

            // 2. Configure the top on-screen crosshair button to smoothly center on the camera position
            map.CustomHomeAction = async () =>
            {
                await Center();
            };

            // Hook HostReady to navigate to camera position and render initial markers & vision cone
            map.HostReady += async (s, e) =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    await map.NavigateToAsync(ddLatPos, ddLonPos, _currentZoom > 1 ? _currentZoom : 13.0);
                    await SetPosition(center: false);
                    await Rotate();
                }
            };

            // 3. Configure the bottom-left style button to open the map & marker styling popup menu
            map.StyleButtonClicked += (s, e) =>
            {
                if (this.contextMenuStrip1 != null && !this.contextMenuStrip1.IsDisposed)
                {
                    _floatingTooltip?.Hide();
                    Point screenPt = map.PointToScreen(new Point(10, map.Height - 46));
                    Point formPt = this.PointToClient(screenPt);
                    this.contextMenuStrip1.Show(this, formPt, ToolStripDropDownDirection.AboveRight);
                }
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
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (map != null && map.IsHostReady)
            {
                await map.NavigateToAsync(ddLatPos, ddLonPos, _currentZoom > 1 ? _currentZoom : 13.0);
                await SetPosition(center: false);
                await Rotate();
            }
        }

        private void MapPanel_PointerMoved(object sender, MapPointerMovedEvent e)
        {
            if (IsDisposed) return;
            if (contextMenuStrip1 != null && contextMenuStrip1.Visible)
            {
                _floatingTooltip?.Hide();
                return;
            }

            double elev = elevationManager != null ? elevationManager.GetElevation(e.Latitude, e.Longitude) : 0;
            var screenPoint = Cursor.Position;
            _floatingTooltip?.UpdateTooltip($"Lat: {e.Latitude:F4}\nLon: {e.Longitude:F4}\nAlt: {elev:F1}m", screenPoint);

            try
            {
                tooltip?.SetText(map, $"Lat: {e.Latitude:F4}\nLon: {e.Longitude:F4}\nAlt: {elev:F1}m");
            }
            catch { }
        }

        private class ProviderItem
        {
            public string Name { get; set; }
            public string Url { get; set; }
        }

        private void InitializeProviderDropdown()
        {
            var cmbProvider = comboBoxMapProvider;
            cmbProvider.Items.Clear();

            cmbProvider.Items.Add(new ProviderItem { Name = "OpenStreetMap", Url = "https://tile.openstreetmap.org/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Offline Only (No Online Map)", Url = "" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Govmap (Hebrew)", Url = "https://cdnil.govmap.gov.il/xyz/heb/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Govmap (English)", Url = "https://cdnil.govmap.gov.il/xyz/eng/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Israel Hiking (Hebrew)", Url = "https://israelhiking.osm.org.il/Hebrew/Tiles/{z}/{x}/{y}.png" });
            cmbProvider.Items.Add(new ProviderItem { Name = "Israel Hiking (English)", Url = "https://israelhiking.osm.org.il/English/Tiles/{z}/{x}/{y}.png" });
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



        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _floatingTooltip?.Hide();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _floatingTooltip?.Dispose();
            base.OnFormClosed(e);
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
            // Target click handled via MapClicked and hit-testing
        }

        private void MapPanel_MapClicked(object sender, MapClickedEvent e)
        {
            lastPoint = (e.Latitude, e.Longitude);
            lastElevation = elevationManager != null ? elevationManager.GetElevation(lastPoint.Lat, lastPoint.Lon) : 0;

            if (e.Button == "Left")
            {
                // Left-click on target -> Goto (Bearing / No Elevation)
                var targetHit = GetTargetAt(e.Latitude, e.Longitude);
                if (targetHit.HasValue)
                {
                    var target = targetHit.Value.Info;
                    lastPoint = (target.Lat, target.Lon);
                    var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
                    double cameraAngle = (bearing + (parent != null ? parent.AzimuthOffset : 0) + 360) % 360;

                    parent?.GotoDeg(cameraAngle, parent != null ? parent.CurrentTilt : 0, false);
                    return;
                }

                // Left-click clears active area selection and hides panel1
                if (_selectedArea != null)
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
        }

        private async void LoadSettings()
        {
            XmlLoader xml = new XmlLoader(PelcoControl.DirectoryPath + PelcoControl.XMLName, true);

            int x = xml.Get("Map/Left", int.MinValue);
            int y = xml.Get("Map/Top", int.MinValue);
            int w = xml.Get("Map/Width", 500);
            int h = xml.Get("Map/Height", 350);

            Screen screen;
            if (x == int.MinValue || y == int.MinValue)
            {
                screen = Screen.PrimaryScreen;
                w = MathE.Clamp(w, 100, screen.Bounds.Width / 2 - 100);
                h = MathE.Clamp(h, 100, screen.Bounds.Height / 2 - 75);
                x = screen.Bounds.X + screen.Bounds.Width / 2 - w / 2;
                y = screen.Bounds.Y + screen.Bounds.Height / 2 - h / 2;
            }
            else
            {
                screen = Screen.FromPoint(new Point(x, y));
                x = MathE.Clamp(x, screen.Bounds.Left, screen.Bounds.Right);
                y = MathE.Clamp(y, screen.Bounds.Top, screen.Bounds.Bottom);
            }

            Left = x;
            Top = y;
            Width = w;
            Height = h;

            bool showDefault = false;
            if (parent != null && (Math.Abs(parent.Latitude) > 0.001 || Math.Abs(parent.Longitude) > 0.001))
            {
                ddLatPos = parent.Latitude;
                ddLonPos = parent.Longitude;
            }
            else
            {
                string pos = xml.Get("Map/Position", "");
                if (!string.IsNullOrEmpty(pos))
                {
                    string[] p = pos.Split(',');
                    if (p.Length >= 2 && double.TryParse(p[0], out double parsedLat) && double.TryParse(p[1], out double parsedLon) && (Math.Abs(parsedLat) > 0.001 || Math.Abs(parsedLon) > 0.001))
                    {
                        ddLatPos = parsedLat;
                        ddLonPos = parsedLon;
                    }
                }
            }

            // Ensure coordinates are on land within Israel region
            if (ddLatPos < 29.0 || ddLatPos > 34.0 || ddLonPos < 34.0 || ddLonPos > 36.0)
            {
                ddLatPos = 31.7767;
                ddLonPos = 35.2345;
            }

            double zoom = xml.Get("Map/Zoom", 0d);
            if (zoom <= 0)
            {
                zoom = 13.0;
            }

            zoom = MathE.Clamp(zoom, 2, 18);
            _currentZoom = zoom;

            // Load icon and color styling from mapStyle.xml
            LoadStyleSettings();

            InitializeIconContextMenu();

            await map.NavigateToAsync(ddLatPos, ddLonPos, zoom);
            await SetPosition(center: false);
            await Rotate();
        }

        private string StyleXmlPath => !string.IsNullOrEmpty(PelcoControl.DirectoryPath)
            ? Path.Combine(PelcoControl.DirectoryPath, "mapStyle.xml")
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mapStyle.xml");

        private void LoadStyleSettings()
        {
            try
            {
                string path = StyleXmlPath;
                if (File.Exists(path))
                {
                    XmlLoader xml = new XmlLoader(path, true);
                    CameraIconType = xml.Get("MapStyle/CameraIconType", "camera_ptz");
                    TargetIconType = xml.Get("MapStyle/TargetIconType", "crosshair");
                    CameraColor = xml.Get("MapStyle/CameraColor", "#00D4FF");
                    TargetColor = xml.Get("MapStyle/TargetColor", "#EF4444");
                    HighlightColor = xml.Get("MapStyle/HighlightColor", "#00E5FF");
                    EnableOfflineMap = xml.Get("MapStyle/EnableOfflineMap", true);
                }
                else
                {
                    CameraIconType = "camera_ptz";
                    TargetIconType = "crosshair";
                    CameraColor = "#00D4FF";
                    TargetColor = "#EF4444";
                    HighlightColor = "#00E5FF";
                    EnableOfflineMap = true;
                }
            }
            catch
            {
            }
        }

        private void SaveStyleSettings()
        {
            try
            {
                string path = StyleXmlPath;
                XmlLoader xml = new XmlLoader(path);
                xml.Set("MapStyle/CameraIconType", CameraIconType);
                xml.Set("MapStyle/TargetIconType", TargetIconType);
                xml.Set("MapStyle/CameraColor", CameraColor);
                xml.Set("MapStyle/TargetColor", TargetColor);
                xml.Set("MapStyle/HighlightColor", HighlightColor);
                xml.Set("MapStyle/EnableOfflineMap", EnableOfflineMap);
                xml.Save();
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            XmlLoader xml = new XmlLoader(PelcoControl.DirectoryPath + PelcoControl.XMLName);

            if (WindowState == FormWindowState.Normal)
            {
                xml.Set("Map/Left", Left);
                xml.Set("Map/Top", Top);
                xml.Set("Map/Width", Width);
                xml.Set("Map/Height", Height);
            }
            else
            {
                xml.Set("Map/Left", RestoreBounds.Left);
                xml.Set("Map/Top", RestoreBounds.Top);
                xml.Set("Map/Width", RestoreBounds.Width);
                xml.Set("Map/Height", RestoreBounds.Height);
            }

            if (Math.Abs(_currentCenterLat) > 0.001 || Math.Abs(_currentCenterLon) > 0.001)
            {
                xml.Set("Map/Position", $"{_currentCenterLat},{_currentCenterLon}");
            }
            if (_currentZoom > 1)
            {
                xml.Set("Map/Zoom", _currentZoom);
            }

            xml.Save();
            SaveStyleSettings();
        }

        private void InitializeIconContextMenu()
        {
            if (this.contextMenuStrip1 == null)
            {
                this.contextMenuStrip1 = new ContextMenuStrip();
            }

            var menu = this.contextMenuStrip1;
            menu.Items.Clear();

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
                        SaveStyleSettings();
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
                    SaveStyleSettings();
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
                        SaveStyleSettings();
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
                    SaveStyleSettings();
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
                    SaveStyleSettings();
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
                        SaveStyleSettings();
                        await SetPosition(false);
                    }
                }
            };
            miCameraColor.DropDownItems.Add(miCustomCameraColor);
            menu.Items.Add(miCameraColor);

            // 5. Offline Map Toggle
            var miOfflineMap = new ToolStripMenuItem("Enable Offline Map (MBTiles)")
            {
                CheckOnClick = true,
                Checked = EnableOfflineMap
            };
            miOfflineMap.Click += (s, e) =>
            {
                EnableOfflineMap = miOfflineMap.Checked;
                SaveStyleSettings();
            };
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miOfflineMap);

            ApplyDimGrayTheme(menu);
        }

        private static void ApplyDimGrayTheme(ContextMenuStrip menu)
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new DimGrayColorTable());
            menu.BackColor = Color.DimGray;
            menu.ForeColor = Color.White;
            menu.ShowImageMargin = false;

            foreach (ToolStripItem item in menu.Items)
            {
                ApplyDimGrayThemeToItem(item);
            }
        }

        private static void ApplyDimGrayThemeToItem(ToolStripItem item)
        {
            item.BackColor = Color.DimGray;
            item.ForeColor = Color.White;

            if (item is ToolStripMenuItem mi)
            {
                mi.DropDown.BackColor = Color.DimGray;
                mi.DropDown.ForeColor = Color.White;
                mi.DropDown.Renderer = new ToolStripProfessionalRenderer(new DimGrayColorTable());
                if (mi.DropDown is ToolStripDropDownMenu menu)
                {
                    menu.ShowImageMargin = false;
                }

                foreach (ToolStripItem subItem in mi.DropDownItems)
                {
                    ApplyDimGrayThemeToItem(subItem);
                }
            }
        }

        private void InitializeActionContextMenu()
        {
            if (map?.ContextMenuOverlay == null) return;
            map.ContextMenuOpening -= Map_ContextMenuOpening;
            map.ContextMenuOpening += Map_ContextMenuOpening;
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

        private void Map_ContextMenuOpening(object sender, ContextMenuOpeningEventArgs e)
        {
            if (map?.ContextMenuOverlay == null) return;
            map.ContextMenuOverlay.ClearItems();

            var targetHit = GetTargetAt(e.Latitude, e.Longitude);
            if (targetHit.HasValue)
            {
                var targetName = targetHit.Value.Name;
                var target = targetHit.Value.Info;

                // 1. Goto Target (With Elevation)
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto",
                    Tooltip = $"Goto '{targetName}' with elevation\nPan Azimuth and Tilt to target position",
                    IconKey = "goto_elevation",
                    AnimationType = IconAnimationType.Elevation3D,
                    AccentColor = Color.FromArgb(0, 229, 255), // Cyan
                    OnClick = (ctx) =>
                    {
                        lastPoint = (target.Lat, target.Lon);
                        lastElevation = elevationManager != null ? elevationManager.GetElevation(lastPoint.Lat, lastPoint.Lon) : 0;
                        double camAlt = (elevationManager != null ? elevationManager.GetElevation(ddLatPos, ddLonPos) : 0) + InstallHeight;
                        var angles = GetAngles(ddLatPos, ddLonPos, camAlt, lastPoint.Lat, lastPoint.Lon, lastElevation);

                        parent?.GotoDeg(angles.Bearing + (parent != null ? parent.AzimuthOffset : 0), angles.Pitch + (parent != null ? parent.ElevationOffset : 0), false);
                    }
                });

                // 2. Goto Target without elevation
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto (Bearing)",
                    Tooltip = $"Goto '{targetName}' without elevation\nPan Horizontal Azimuth only (Keep Current Tilt)",
                    IconKey = "goto_bearing",
                    AnimationType = IconAnimationType.CompassBearing,
                    AccentColor = Color.FromArgb(245, 158, 11), // Amber
                    OnClick = (ctx) =>
                    {
                        lastPoint = (target.Lat, target.Lon);
                        var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
                        double cameraAngle = (bearing + (parent != null ? parent.AzimuthOffset : 0) + 360) % 360;

                        parent?.GotoDeg(cameraAngle, parent != null ? parent.CurrentTilt : 0, false);
                    }
                });

                // 3. Rename Target
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
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
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
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
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto",
                    Tooltip = "Goto (With Elevation)\nCalculate Bearing & Pitch Angle using Elevation Data",
                    IconKey = "goto_elevation",
                    AnimationType = IconAnimationType.Elevation3D,
                    AccentColor = Color.FromArgb(0, 229, 255), // Cyan
                    OnClick = (ctx) =>
                    {
                        lastPoint = (ctx.Latitude, ctx.Longitude);
                        lastElevation = elevationManager != null ? elevationManager.GetElevation(lastPoint.Lat, lastPoint.Lon) : 0;
                        double camAlt = (elevationManager != null ? elevationManager.GetElevation(ddLatPos, ddLonPos) : 0) + InstallHeight;
                        var angles = GetAngles(ddLatPos, ddLonPos, camAlt, lastPoint.Lat, lastPoint.Lon, lastElevation);

                        parent?.GotoDeg(angles.Bearing + (parent != null ? parent.AzimuthOffset : 0), angles.Pitch + (parent != null ? parent.ElevationOffset : 0), false);
                    }
                });

                // 2. Goto without elevation
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
                {
                    Name = "Goto without elevation",
                    Tooltip = "Goto without elevation\nPan Horizontal Azimuth only (Keep Current Tilt)",
                    IconKey = "goto_bearing",
                    AnimationType = IconAnimationType.CompassBearing,
                    AccentColor = Color.FromArgb(245, 158, 11), // Amber
                    OnClick = (ctx) =>
                    {
                        lastPoint = (ctx.Latitude, ctx.Longitude);
                        var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
                        double cameraAngle = (bearing + (parent != null ? parent.AzimuthOffset : 0) + 360) % 360;

                        parent?.GotoDeg(cameraAngle, parent != null ? parent.CurrentTilt : 0, false);
                    }
                });

                // 3. Add Target (Multi-Target)
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
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
                map.ContextMenuOverlay.AddItem(new MapContextMenuItem
                {
                    Name = "Set Camera",
                    Tooltip = "Set Camera Position\nMove camera origin to this location",
                    IconKey = "camera_ptz",
                    AnimationType = IconAnimationType.Pulse,
                    AccentColor = Color.FromArgb(34, 197, 94), // Green
                    OnClick = async (ctx) =>
                    {
                        ddLatPos = ctx.Latitude;
                        ddLonPos = ctx.Longitude;
                        SaveSettings();
                        await SetPosition(center: false);
                    }
                });
            }
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
            _cameraMarkerId = await map.AddPointAsync("Camera", ddLatPos, ddLonPos, label: "Camera", color: CameraColor, scale: 1.0, rotation: rotation, iconType: CameraIconType, featureId: "CameraMarker");

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
                    await map.RemoveFeatureAsync("Targets", existing.FeatureId);
            }

            string id = await map.AddPointAsync("Targets", target[0], target[1], label: name, color: color ?? TargetColor, scale: 1.2, iconType: iconType ?? TargetIconType);
            Targets[name] = new TargetInfo { FeatureId = id, Lat = target[0], Lon = target[1], IsHighlighted = false };
        }

        public async Task DeleteTarget(string name)
        {
            if (Targets.TryGetValue(name, out var t))
            {
                Targets.Remove(name);
                if (!string.IsNullOrEmpty(t.FeatureId))
                {
                    await map.RemoveFeatureAsync("Targets", t.FeatureId);
                }
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
                await map.RemoveFeatureAsync("Targets", t.FeatureId);
            }

            string col = t.IsHighlighted ? HighlightColor : TargetColor;
            t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: newName, color: col, scale: 1.2, iconType: TargetIconType);
            Targets[newName] = t;
            return true;
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

            // 1. Update Camera Marker with active pan rotation in-place
            _cameraMarkerId = await map.AddPointAsync("Camera", ddLatPos, ddLonPos, label: "Camera", color: CameraColor, scale: 1.0, rotation: camPan, iconType: CameraIconType, featureId: "CameraMarker");

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

            if (visionCoords != null && visionCoords.Length >= 3)
            {
                await map.AddPolygonAsync("Vision", visionCoords, fillColor: "#3C8B0000", outlineColor: "#8B0000", outlineWidth: 1.5, featureId: "CameraVisionCone");
            }
            else
            {
                await map.ClearLayerAsync("Vision");
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

        private void frmMap_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            SaveSettings();
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

            parent.GotoDeg(angles.Bearing + parent.AzimuthOffset, angles.Pitch + parent.ElevationOffset, false);
        }

        private void miGotoNoElevation_Click(object sender, EventArgs e)
        {
            var bearing = CalculateBearing(ddLatPos, ddLonPos, lastPoint.Lat, lastPoint.Lon);
            double cameraAngle = (bearing + parent.AzimuthOffset + 360) % 360;

            parent.GotoDeg(cameraAngle, parent.CurrentTilt, false);
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
            return FindGroundIntersectionStatic(start, startAlt, bearing, pitch, vfov, stepMeters, maxRangeMeters, elevationManager);
        }

        public static ((double Lat, double Lon) Start, (double Lat, double Lon) Stop) FindGroundIntersectionStatic(
            (double Lat, double Lon) start, 
            double startAlt, 
            double bearing, 
            double pitch, 
            double vfov, 
            double stepMeters, 
            double maxRangeMeters,
            ElevationManager elevMgr = null)
        {
            double topAngle = pitch + (vfov / 2.0);
            double botAngle = pitch - (vfov / 2.0);
            double centerAngle = pitch;

            double topTan = Math.Tan(topAngle * Math.PI / 180.0);
            double botTan = Math.Tan(botAngle * Math.PI / 180.0);
            double centerTan = Math.Tan(centerAngle * Math.PI / 180.0);

            double bearingRad = bearing * Math.PI / 180.0;
            double lat1Rad = start.Lat * Math.PI / 180.0;
            double lon1Rad = start.Lon * Math.PI / 180.0;

            (double Lat, double Lon) startIntersection = (0, 0);
            bool foundStart = false;

            double originGroundAlt = (elevMgr != null) ? elevMgr.GetElevation(start.Lat, start.Lon) : 0;
            if (startAlt <= originGroundAlt + 1.0)
            {
                foundStart = true;
                startIntersection = start;
            }

            for (double d = stepMeters; d < maxRangeMeters; d += stepMeters)
            {
                double currLatRad = Math.Asin(Math.Sin(lat1Rad) * Math.Cos(d / R) +
                                    Math.Cos(lat1Rad) * Math.Sin(d / R) * Math.Cos(bearingRad));
                double currLonRad = lon1Rad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(d / R) * Math.Cos(lat1Rad),
                                    Math.Cos(d / R) - Math.Sin(lat1Rad) * Math.Sin(currLatRad));

                double currLat = currLatRad * 180.0 / Math.PI;
                double currLon = currLonRad * 180.0 / Math.PI;

                double visionTopAlt = startAlt + (d * topTan);
                double visionBotAlt = startAlt + (d * botTan);
                double visionCenterAlt = startAlt + (d * centerTan);

                double groundAlt = (elevMgr != null) ? elevMgr.GetElevation(currLat, currLon) : 0;

                // 1. Near ground hit: bottom ray intersects the ground
                if (!foundStart)
                {
                    if (visionBotAlt <= groundAlt)
                    {
                        foundStart = true;
                        startIntersection = (currLat, currLon);
                    }
                }
                else
                {
                    // 2. Far ground hit & Terrain occlusion:
                    if (visionTopAlt <= groundAlt)
                    {
                        return (startIntersection, (currLat, currLon));
                    }

                    if (centerAngle < 0 && visionCenterAlt <= groundAlt)
                    {
                        return (startIntersection, (currLat, currLon));
                    }

                    if (groundAlt >= visionTopAlt || (groundAlt >= visionCenterAlt && groundAlt > originGroundAlt))
                    {
                        return (startIntersection, (currLat, currLon));
                    }
                }
            }

            return (startIntersection, GetPointAtDistanceStatic(start, bearing, maxRangeMeters));
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
            double vFov = (HRes > 0 && VRes > 0) ? fov * (VRes / HRes) : fov * (9.0 / 16.0);
            return CreateVisionTriangleStatic(originLat, originLon, alt, bearing, pitch, fov, vFov, stepMeters, maxRangeMeters, elevationManager);
        }

        public static double[][] CreateVisionTriangleStatic(
            double originLat, 
            double originLon, 
            double alt, 
            double bearing, 
            double pitch, 
            double fov = 30, 
            double? vfov = null,
            double stepMeters = 20, 
            double maxRangeMeters = 50000,
            ElevationManager elevMgr = null)
        {
            var nearPoints = new List<(double Lat, double Lon)>();
            var farPoints = new List<(double Lat, double Lon)>();
            double effectiveVfov = vfov ?? (fov * (9.0 / 16.0));

            double fovStep = Math.Max(0.5, fov * 0.05);
            for (double a = bearing - (fov / 2.0); a <= bearing + (fov / 2.0) + 0.0001; a += fovStep)
            {
                var hitPoint = FindGroundIntersectionStatic((originLat, originLon), alt, a, pitch, effectiveVfov, stepMeters, maxRangeMeters, elevMgr);
                if (Math.Abs(hitPoint.Start.Lat) > 0.0001 || Math.Abs(hitPoint.Start.Lon) > 0.0001)
                {
                    nearPoints.Add(hitPoint.Start);
                }
                else
                {
                    nearPoints.Add((originLat, originLon));
                }
                farPoints.Add(hitPoint.Stop);
            }

            if (farPoints.Count < 2) return null;

            // Form closed polygon ring: near edge (left to right) -> far edge (right to left) -> close
            var points = new List<(double Lat, double Lon)>();
            bool allAtOrigin = nearPoints.All(p => Math.Abs(p.Lat - originLat) < 0.00001 && Math.Abs(p.Lon - originLon) < 0.00001);
            if (allAtOrigin)
            {
                points.Add((originLat, originLon));
                points.AddRange(farPoints);
                points.Add((originLat, originLon));
            }
            else
            {
                points.AddRange(nearPoints);
                farPoints.Reverse();
                points.AddRange(farPoints);
                points.Add(nearPoints[0]);
            }

            return points.Select(p => new double[] { p.Lat, p.Lon }).ToArray();
        }

        private (double Lat, double Lon) GetPointAtDistance((double Lat, double Lon) start, double brngDeg, double dist)
        {
            return GetPointAtDistanceStatic(start, brngDeg, dist);
        }

        public static (double Lat, double Lon) GetPointAtDistanceStatic((double Lat, double Lon) start, double brngDeg, double dist)
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
