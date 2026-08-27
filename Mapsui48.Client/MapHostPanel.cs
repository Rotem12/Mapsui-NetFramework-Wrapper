using Mapsui48.Protocol;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mapsui48.Client
{
    public class MapHostPanel : Panel
    {
        private MapsuiHostClient _client;
        private bool _isLoaded;

        public string MBTilesPath { get; set; }
        public string OnlineUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        public string CachePath { get; set; }
        public string HostExePath { get; set; }

        public event EventHandler<MapClickedEvent> MapClicked;
        public event EventHandler<MapDoubleClickedEvent> MapDoubleClicked;
        public event EventHandler<FeatureClickedEvent> FeatureClicked;
        public event EventHandler<ViewportChangedEvent> ViewportChanged;
        public event EventHandler<AreaSelectedEvent> AreaSelected;
        public event EventHandler<MapPointerMovedEvent> PointerMoved;
        public event EventHandler<ContextMenuOpeningEventArgs> ContextMenuOpening;
        public event EventHandler<MapPointerLeftEvent> PointerLeft;
        
        public event Func<BoundingBox, int, int, System.Threading.CancellationToken, Task> OnDownloadStarted;

        public bool EnableBuiltInDownloadOverlay { get; set; } = false;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_CHILD = 5;

        public Func<Task> CustomHomeAction { get; set; }

        public MapContextMenuOverlay ContextMenuOverlay { get; }
        public bool EnableContextMenuOverlay { get; set; } = true;

        public int ContextMenuButtonSize
        {
            get => ContextMenuOverlay?.ButtonSize ?? 42;
            set { if (ContextMenuOverlay != null) ContextMenuOverlay.ButtonSize = value; }
        }

        public float ContextMenuIconSizeFactor
        {
            get => ContextMenuOverlay?.IconSizeFactor ?? 1.0f;
            set { if (ContextMenuOverlay != null) ContextMenuOverlay.IconSizeFactor = value; }
        }

        public MapHostPanel()
        {
            ContextMenuOverlay = new MapContextMenuOverlay(this);
            _client = new MapsuiHostClient();
            _client.MapClicked += (s, e) => 
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (e.Button == "Right")
                            {
                                var screenPt = PointToScreen(new System.Drawing.Point((int)e.ScreenX, (int)e.ScreenY));
                                var args = new ContextMenuOpeningEventArgs(screenPt, e.Latitude, e.Longitude);
                                ContextMenuOpening?.Invoke(this, args);

                                if (!args.Cancel)
                                {
                                    if (EnableContextMenuOverlay && ContextMenuOverlay != null && ContextMenuOverlay.Items.Count > 0)
                                    {
                                        ContextMenuOverlay.ShowAt(screenPt, e.Latitude, e.Longitude);
                                    }
                                    else if (this.ContextMenuStrip != null)
                                    {
                                        this.ContextMenuStrip.Show(this, new System.Drawing.Point((int)e.ScreenX, (int)e.ScreenY));
                                    }
                                }
                            }
                            else if (e.Button == "Left")
                            {
                                if (ContextMenuOverlay != null && ContextMenuOverlay.Visible && (DateTime.UtcNow - ContextMenuOverlay.ShowTime).TotalMilliseconds < 600)
                                {
                                    // Ignore spurious Left click right after right click opened the menu
                                    return;
                                }
                                ContextMenuOverlay?.HideMenu();
                            }
                        });
                    }
                    catch { }
                }
                MapClicked?.Invoke(this, e);
            };
            _client.MapDoubleClicked += (s, e) => 
            {
                ContextMenuOverlay?.HideMenu();
                MapDoubleClicked?.Invoke(this, e);
            };
            _client.FeatureClicked += (s, e) => FeatureClicked?.Invoke(this, e);
            _client.ViewportChanged += (s, e) => 
            {
                CurrentZoom = e.ZoomLevel;
                ViewportChanged?.Invoke(this, e);
            };

            _client.PointerMoved += (s, e) => 
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            PointerMoved?.Invoke(this, e);
                        });
                        return;
                    }
                    catch { }
                }
                PointerMoved?.Invoke(this, e);
            };
            _client.PointerLeft += (s, e) => 
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            PointerLeft?.Invoke(this, e);
                        });
                        return;
                    }
                    catch { }
                }
                PointerLeft?.Invoke(this, e);
            };
            _client.AreaSelected += (s, e) => 
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (EnableBuiltInDownloadOverlay)
                            {
                                ShowDownloadOverlay(e);
                            }
                            AreaSelected?.Invoke(this, e);
                        });
                        return;
                    }
                    catch { }
                }
                AreaSelected?.Invoke(this, e);
            };
        }

        private Panel _downloadOverlay;

        private void ShowDownloadOverlay(AreaSelectedEvent e)
        {
            if (_downloadOverlay != null)
            {
                this.Controls.Remove(_downloadOverlay);
                _downloadOverlay.Dispose();
            }

            _downloadOverlay = new Panel
            {
                Width = 260,
                Height = 150,
                BackColor = System.Drawing.Color.FromArgb(240, 25, 25, 25), // Dark theme
                ForeColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            int x = e.ScreenX + e.ScreenWidth + 10;
            if (x + _downloadOverlay.Width > this.Width)
            {
                x = e.ScreenX - _downloadOverlay.Width - 10;
                if (x < 0) x = 10;
            }
            int y = e.ScreenY;
            if (y + _downloadOverlay.Height > this.Height)
            {
                y = this.Height - _downloadOverlay.Height - 10;
                if (y < 0) y = 10;
            }
            
            _downloadOverlay.Location = new System.Drawing.Point(x, y);

            var lblTitle = new Label
            {
                Text = "Offline Tile Downloader",
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };

            var lblZoom = new Label
            {
                Text = "Max Zoom:",
                Location = new System.Drawing.Point(10, 37),
                AutoSize = true
            };

            var numZoom = new NumericUpDown
            {
                Location = new System.Drawing.Point(85, 35),
                Width = 60,
                Minimum = 0,
                Maximum = 18,
                Value = 14,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblEta = new Label
            {
                Text = "Ready",
                Location = new System.Drawing.Point(10, 65),
                AutoSize = true,
                ForeColor = System.Drawing.Color.LightGray
            };

            var progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(10, 85),
                Width = 240,
                Height = 15,
                Style = ProgressBarStyle.Continuous
            };

            var btnDownload = new Button
            {
                Text = "Download",
                Location = new System.Drawing.Point(10, 110),
                Width = 115,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 122, 204),
                ForeColor = System.Drawing.Color.White,
                Cursor = Cursors.Hand
            };
            btnDownload.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(135, 110),
                Width = 115,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnCancel.Click += (s, ev) => 
            {
                this.Controls.Remove(_downloadOverlay);
                _downloadOverlay.Dispose();
                _downloadOverlay = null;
            };

            var bbox = new BoundingBox { MinLat = e.MinLat, MinLon = e.MinLon, MaxLat = e.MaxLat, MaxLon = e.MaxLon };

            System.Threading.CancellationTokenSource cts = null;

            btnDownload.Click += async (s, ev) => 
            {
                if (btnDownload.Text == "Download")
                {
                    btnDownload.Text = "Cancel";
                    btnDownload.BackColor = System.Drawing.Color.FromArgb(204, 50, 50);
                    lblEta.Text = "Starting download...";
                    progressBar.Style = ProgressBarStyle.Marquee;
                    numZoom.Enabled = false;

                    cts = new System.Threading.CancellationTokenSource();

                    try
                    {
                        if (OnDownloadStarted != null)
                        {
                            await OnDownloadStarted(bbox, 0, (int)numZoom.Value, cts.Token);
                            lblEta.Text = "Download Complete!";
                            progressBar.Style = ProgressBarStyle.Continuous;
                            progressBar.Value = 100;
                        }
                        else 
                        {
                            lblEta.Text = "No handler assigned.";
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        lblEta.Text = "Cancelled by user.";
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progressBar.Value = 0;
                    }
                    catch (Exception ex)
                    {
                        lblEta.Text = "Error: " + ex.Message;
                        progressBar.Style = ProgressBarStyle.Continuous;
                    }
                    finally
                    {
                        btnDownload.Text = "Done";
                        btnDownload.BackColor = System.Drawing.Color.Green;
                    }
                }
                else if (btnDownload.Text == "Cancel")
                {
                    cts?.Cancel();
                    btnDownload.Enabled = false;
                    lblEta.Text = "Cancelling...";
                }
                else if (btnDownload.Text == "Done")
                {
                    btnCancel.PerformClick();
                }
            };

            _downloadOverlay.Controls.Add(lblTitle);
            _downloadOverlay.Controls.Add(lblZoom);
            _downloadOverlay.Controls.Add(numZoom);
            _downloadOverlay.Controls.Add(lblEta);
            _downloadOverlay.Controls.Add(progressBar);
            _downloadOverlay.Controls.Add(btnDownload);
            _downloadOverlay.Controls.Add(btnCancel);

            this.Controls.Add(_downloadOverlay);
            _downloadOverlay.BringToFront();
        }

        public double CurrentZoom { get; private set; }
        private MapOverlayUI _overlayUI;
        private MapStyleButtonOverlay _styleOverlay;

        public event EventHandler StyleButtonClicked;

        private readonly TaskCompletionSource<bool> _readyTcs = new TaskCompletionSource<bool>();
        public Task WhenReadyAsync() => _readyTcs.Task;
        public bool IsHostReady { get; private set; }
        public event EventHandler HostReady;

        protected override async void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            
            if (!DesignMode && !_isLoaded)
            {
                _isLoaded = true;
                try
                {
                    await _client.StartAsync(this.Handle, HostExePath, MBTilesPath, OnlineUrl, CachePath);
                    
                    _overlayUI = new MapOverlayUI(this);
                    _overlayUI.AttachEvents();

                    _styleOverlay = new MapStyleButtonOverlay(this);
                    _styleOverlay.StyleClicked += (s, ev) => StyleButtonClicked?.Invoke(this, EventArgs.Empty);
                    _styleOverlay.AttachEvents();

                    IsHostReady = true;
                    _readyTcs.TrySetResult(true);
                    HostReady?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    _readyTcs.TrySetException(ex);
                    MessageBox.Show("Failed to initialize Map Host: " + ex.Message);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_CONTEXTMENU = 0x007B;
            if (m.Msg == WM_CONTEXTMENU)
            {
                if (EnableContextMenuOverlay && ContextMenuOverlay != null && ContextMenuOverlay.Items.Count > 0)
                {
                    // Suppress default WinForms context menu popup so it doesn't collide with the HUD overlay
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Use MoveWindow to directly resize the embedded child HWND.
            // This is more reliable than WM_SIZE for cross-process WPF child windows.
            IntPtr child = GetWindow(this.Handle, GW_CHILD);
            if (child != IntPtr.Zero)
            {
                MoveWindow(child, 0, 0, this.ClientSize.Width, this.ClientSize.Height, true);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
                _overlayUI?.Dispose();
                _styleOverlay?.Dispose();
                ContextMenuOverlay?.Dispose();
            }
            base.Dispose(disposing);
        }

        // Delegate API methods to the client, automatically ensuring the Host is ready
        public async Task NavigateToAsync(double lat, double lon, double? zoom = null, int? durationMs = null)
        {
            await WhenReadyAsync();
            await _client.NavigateToAsync(lat, lon, zoom, durationMs);
        }

        public async Task FlyToAsync(double lat, double lon, double? zoom = null, int? durationMs = null)
        {
            await WhenReadyAsync();
            await _client.FlyToAsync(lat, lon, zoom, durationMs);
        }

        public async Task SetZoomAsync(double zoomLevel, int? durationMs = null)
        {
            await WhenReadyAsync();
            await _client.SetZoomAsync(zoomLevel, durationMs);
        }

        public async Task GoHomeAsync(int? durationMs = null)
        {
            await WhenReadyAsync();
            await _client.GoHomeAsync(durationMs);
        }

        public async Task ChangeOnlineProviderAsync(string onlineUrl)
        {
            this.OnlineUrl = onlineUrl;
            if (_client != null)
            {
                try
                {
                    await WhenReadyAsync();
                    await _client.SetTileSourceAsync(this.MBTilesPath, this.OnlineUrl, this.CachePath);
                }
                catch { }
            }
        }

        public async Task LoadVectorTileAsync(string mbTilesPath)
        {
            await WhenReadyAsync();
            await _client.LoadVectorTileAsync(mbTilesPath);
        }

        public async Task<string> AddPolygonAsync(string layer, double[][] coordinates, string fillColor = "#800000FF", string outlineColor = "#FF0000FF", double outlineWidth = 2, string featureId = null)
        {
            await WhenReadyAsync();
            return await _client.AddPolygonAsync(layer, coordinates, fillColor, outlineColor, outlineWidth, featureId);
        }

        public async Task<string> AddCircleAsync(string layer, double centerLat, double centerLon, double radiusMeters, string fillColor = null, string outlineColor = "#3B82F6", double outlineWidth = 2.0, double[] dashArray = null, int segments = 64, string featureId = null)
        {
            await WhenReadyAsync();
            return await _client.AddCircleAsync(layer, centerLat, centerLon, radiusMeters, fillColor, outlineColor, outlineWidth, dashArray, segments, featureId);
        }

        public async Task<string> AddPointAsync(string layer, double lat, double lon, string label = null, string color = "#FFFF0000", double scale = 1.0, double? rotation = null, string iconType = null, string featureId = null)
        {
            await WhenReadyAsync();
            return await _client.AddPointAsync(layer, lat, lon, label, color, scale, rotation, iconType, featureId);
        }

        public async Task<string> AddMarkerAsync(string layer, double lat, double lon, string label = null, string iconType = "pin", string color = "#00E5FF", double scale = 1.0, double? rotation = null, string featureId = null)
        {
            return await AddPointAsync(layer, lat, lon, label, color, scale, rotation, iconType, featureId);
        }

        public async Task<string> AddTargetAsync(double lat, double lon, string label = "Target", string iconType = "crosshair", string color = "#EF4444", double scale = 1.2, double? rotation = null, string featureId = null)
        {
            return await AddPointAsync("Targets", lat, lon, label, color, scale, rotation, iconType, featureId);
        }

        public async Task<string> SetCameraMarkerAsync(double lat, double lon, string label = "Camera", string iconType = "camera_ptz", string color = "#00D4FF", double scale = 1.0, double? rotation = null, string featureId = "CameraMarker")
        {
            return await AddPointAsync("Camera", lat, lon, label, color, scale, rotation, iconType, featureId);
        }

        public async Task<string> AddLineAsync(string layer, double[][] coordinates, string color = "#FF0000FF", double width = 2, string featureId = null)
        {
            await WhenReadyAsync();
            return await _client.AddLineAsync(layer, coordinates, color, width, featureId);
        }

        public async Task RemoveFeatureAsync(string layer, string featureId)
        {
            await WhenReadyAsync();
            await _client.RemoveFeatureAsync(layer, featureId);
        }

        public async Task ClearLayerAsync(string layer)
        {
            await WhenReadyAsync();
            await _client.ClearLayerAsync(layer);
        }

        // ── Navigation & Camera Controls ─────────────────────────────

        public async Task RotateToAsync(double heading, int? durationMs = null, string easing = null)
        {
            await WhenReadyAsync();
            await _client.RotateToAsync(heading, durationMs, easing);
        }

        public async Task SetRotationLockAsync(bool locked)
        {
            await WhenReadyAsync();
            await _client.SetRotationLockAsync(locked);
        }

        public async Task ZoomToBoxAsync(double minLat, double minLon, double maxLat, double maxLon, int? durationMs = null, string boxFit = "Fit")
        {
            await WhenReadyAsync();
            await _client.ZoomToBoxAsync(minLat, minLon, maxLat, maxLon, durationMs, boxFit);
        }

        public async Task SetViewportBoundsAsync(double? minLat = null, double? minLon = null, double? maxLat = null, double? maxLon = null, double? minZoom = null, double? maxZoom = null)
        {
            await WhenReadyAsync();
            await _client.SetViewportBoundsAsync(minLat, minLon, maxLat, maxLon, minZoom, maxZoom);
        }

        public async Task SetPanLockAsync(bool locked)
        {
            await WhenReadyAsync();
            await _client.SetPanLockAsync(locked);
        }

        public async Task SetZoomLockAsync(bool locked)
        {
            await WhenReadyAsync();
            await _client.SetZoomLockAsync(locked);
        }

        // ── Layer Management ─────────────────────────────────────────

        public async Task SetLayerVisibilityAsync(string layer, bool visible)
        {
            await WhenReadyAsync();
            await _client.SetLayerVisibilityAsync(layer, visible);
        }

        public async Task SetLayerOpacityAsync(string layer, double opacity)
        {
            await WhenReadyAsync();
            await _client.SetLayerOpacityAsync(layer, opacity);
        }

        public async Task SetLayerScaleRangeAsync(string layer, double? minZoom = null, double? maxZoom = null)
        {
            await WhenReadyAsync();
            await _client.SetLayerScaleRangeAsync(layer, minZoom, maxZoom);
        }

        public async Task RemoveLayerAsync(string layer)
        {
            await WhenReadyAsync();
            await _client.RemoveLayerAsync(layer);
        }

        public async Task<System.Collections.Generic.List<LayerInfoDto>> GetLayersAsync()
        {
            await WhenReadyAsync();
            return await _client.GetLayersAsync();
        }

        // ── Batch & Advanced Features ────────────────────────────────

        public async Task<System.Collections.Generic.List<string>> AddFeaturesBatchAsync(string layer, System.Collections.Generic.IEnumerable<FeatureDto> features)
        {
            await WhenReadyAsync();
            return await _client.AddFeaturesBatchAsync(layer, features);
        }

        public async Task UpdateFeatureAsync(string layer, string featureId, double? lat = null, double? lon = null, double? rotation = null, double? scale = null, string label = null)
        {
            await WhenReadyAsync();
            await _client.UpdateFeatureAsync(layer, featureId, lat, lon, rotation, scale, label);
        }

        public async Task ShowCalloutAsync(string layer, string featureId, string title, string subtitle = null, bool enabled = true)
        {
            await WhenReadyAsync();
            await _client.ShowCalloutAsync(layer, featureId, title, subtitle, enabled);
        }

        // ── Canvas HUD Widgets ───────────────────────────────────────

        public async Task SetScaleBarWidgetAsync(bool enabled, string position = "BottomLeft", string mode = "Single")
        {
            await WhenReadyAsync();
            await _client.SetScaleBarWidgetAsync(enabled, position, mode);
        }

        public async Task SetMouseCoordinatesWidgetAsync(bool enabled, string position = "BottomRight")
        {
            await WhenReadyAsync();
            await _client.SetMouseCoordinatesWidgetAsync(enabled, position);
        }

        public async Task SetPerformanceWidgetAsync(bool enabled, string position = "TopRight")
        {
            await WhenReadyAsync();
            await _client.SetPerformanceWidgetAsync(enabled, position);
        }

        public async Task SetZoomButtonsWidgetAsync(bool enabled, string position = "TopLeft")
        {
            await WhenReadyAsync();
            await _client.SetZoomButtonsWidgetAsync(enabled, position);
        }

        public async Task SetRulerWidgetAsync(bool enabled)
        {
            await WhenReadyAsync();
            await _client.SetRulerWidgetAsync(enabled);
        }

        // ── GIS Data Loaders & Formats ────────────────────────────────

        public async Task LoadGeoJsonAsync(string geoJsonOrFilePath, string layerName = "GeoJsonLayer", string fillColor = "#403B82F6", string outlineColor = "#3B82F6", double outlineWidth = 2.0)
        {
            await WhenReadyAsync();
            await _client.LoadGeoJsonAsync(geoJsonOrFilePath, layerName, fillColor, outlineColor, outlineWidth);
        }

        public async Task LoadShapefileAsync(string shapefilePath, string layerName = "ShapefileLayer", string fillColor = "#4010B981", string outlineColor = "#10B981", double outlineWidth = 2.0)
        {
            await WhenReadyAsync();
            await _client.LoadShapefileAsync(shapefilePath, layerName, fillColor, outlineColor, outlineWidth);
        }

        public async Task AddWmsLayerAsync(string url, string layerName = "WmsLayer", string serviceLayerName = null, string crs = "EPSG:3857")
        {
            await WhenReadyAsync();
            await _client.AddWmsLayerAsync(url, layerName, serviceLayerName, crs);
        }

        // ── Coordinate Translation & Spatial Queries ─────────────────

        public async Task<CoordinateResultDto> ScreenToWorldAsync(double screenX, double screenY)
        {
            await WhenReadyAsync();
            return await _client.ScreenToWorldAsync(screenX, screenY);
        }

        public async Task<CoordinateResultDto> WorldToScreenAsync(double lat, double lon)
        {
            await WhenReadyAsync();
            return await _client.WorldToScreenAsync(lat, lon);
        }

        public async Task<BoundsResultDto> GetLayerBoundsAsync(string layerName)
        {
            await WhenReadyAsync();
            return await _client.GetLayerBoundsAsync(layerName);
        }

        // ── Animated Glide Tracking ──────────────────────────────────

        public async Task<string> AddAnimatedPointAsync(string layerName, double lat, double lon, int durationMs = 1000, string featureId = null, string label = null, string color = null, double scale = 1.0, double? rotation = null, string iconType = null)
        {
            await WhenReadyAsync();
            return await _client.AddAnimatedPointAsync(layerName, lat, lon, durationMs, featureId, label, color, scale, rotation, iconType);
        }

        public async Task UpdateAnimatedPointAsync(string layerName, string featureId, double lat, double lon, int durationMs = 1000, double? rotation = null, double? scale = null, string label = null)
        {
            await WhenReadyAsync();
            await _client.UpdateAnimatedPointAsync(layerName, featureId, lat, lon, durationMs, rotation, scale, label);
        }

        // ── Mouse & Pointer Event Controls ───────────────────────────

        public async Task SetPointerMoveEventsAsync(bool enabled)
        {
            await WhenReadyAsync();
            await _client.SetPointerMoveEventsAsync(enabled);
        }

        // ── Snapshot & Utilities ─────────────────────────────────────

        public async Task<byte[]> GetSnapshotAsync(string format = "Png", int quality = 100)
        {
            await WhenReadyAsync();
            return await _client.GetSnapshotAsync(format, quality);
        }
    }

    public class ContextMenuOpeningEventArgs : EventArgs
    {
        public System.Drawing.Point ScreenPoint { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool Cancel { get; set; }

        public ContextMenuOpeningEventArgs(System.Drawing.Point screenPt, double lat, double lon)
        {
            ScreenPoint = screenPt;
            Latitude = lat;
            Longitude = lon;
        }
    }
}


