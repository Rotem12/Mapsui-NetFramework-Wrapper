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
        private CancellationTokenSource cts;
        private DateTime startTime;

        private const double R = 6371000; // Earth radius in meters

        private AreaSelectedEvent _selectedArea;

        public frmMap(PelcoControl pelco)
        {
            InitializeComponent();
            parent = pelco;
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

            // 1. Attach Right-Click Context Menu if available on form
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
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                double elev = elevationManager != null ? elevationManager.GetElevation(e.Latitude, e.Longitude) : 0;
                tooltip.SetText(map, $"Lat: {e.Latitude:F4}\nLon: {e.Longitude:F4}\nAlt: {elev:F1}m");
            });
        }

        // Area Selection Event -> Shows the Downloader Panel (panel1)
        private void MapPanel_AreaSelected(object sender, AreaSelectedEvent e)
        {
            _selectedArea = e;
            if (!IsHandleCreated || IsDisposed) return;
            Invoke((MethodInvoker)delegate
            {
                if (panel1 != null)
                {
                    panel1.Visible = true;
                    panel1.BringToFront();
                }
            });
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
            string pos = xml.Get("Map/Position", "");
            if (!string.IsNullOrEmpty(pos))
            {
                string[] p = pos.Split(',');
                ddLatPos = double.Parse(p[0]);
                ddLonPos = double.Parse(p[1]);
            }
            else
            {
                showDefault = true;
                ddLatPos = 31.5;
                ddLonPos = 34.75;
            }

            double zoom = xml.Get("Map/Zoom", 0d);
            if (zoom <= 0)
            {
                zoom = GetZoomForRadiusKm(showDefault ? 235 : 5, ddLatPos);
            }

            zoom = MathE.Clamp(zoom, 2, 18);
            _currentZoom = zoom;

            // Automatically awaits host readiness internally, ensuring the map is centered on opening
            await SetPosition(center: true);
            await map.SetZoomAsync(zoom);
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

            xml.Set("Map/Position", $"{_currentCenterLat},{_currentCenterLon}");
            xml.Set("Map/Zoom", _currentZoom);
            xml.Save();
        }

        // ==========================================
        // Camera Positioning
        // ==========================================

        public async Task SetPosition(bool center = false)
        {
            await map.ClearLayerAsync("Camera");
            _cameraMarkerId = await map.AddPointAsync("Camera", ddLatPos, ddLonPos, label: "Camera", color: "#0000FF", scale: 1.5);

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
            await map.FlyToAsync(ddLatPos, ddLonPos, _currentZoom, durationMs: 600);
        }

        // ==========================================
        // Target Tracking Management
        // ==========================================

        public async Task ShowTarget(double[] target)
        {
            if (target == null || target.Length < 2) return;

            await ClearTarget();
            string id = await map.AddPointAsync("Targets", target[0], target[1], label: "Target", color: "#FF0000", scale: 1.5);
            Targets["MainTarget"] = new TargetInfo { FeatureId = id, Lat = target[0], Lon = target[1] };

            EnsurePointIsVisible(target[0], target[1]);
        }

        public async Task AddTarget(string name, double[] target)
        {
            if (target == null || target.Length < 2) return;

            if (Targets.TryGetValue(name, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.FeatureId))
                    await map.RemoveFeatureAsync("Targets", existing.FeatureId);
            }

            string id = await map.AddPointAsync("Targets", target[0], target[1], label: name, color: "#FF0000", scale: 1.2);
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

                t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: name, color: "#00E5FF", scale: 1.6);
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

                t.FeatureId = await map.AddPointAsync("Targets", t.Lat, t.Lon, label: name, color: "#FF0000", scale: 1.2);
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

            double[][] visionCoords = CreateVisionTriangle(
                ddLatPos, 
                ddLonPos, 
                camAlt, 
                parent.CurrentPanNorthed, 
                parent.CurrentTilt, 
                parent.CurrentZoom, 
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
