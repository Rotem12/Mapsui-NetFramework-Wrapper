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
        public event EventHandler<FeatureClickedEvent> FeatureClicked;
        public event EventHandler<ViewportChangedEvent> ViewportChanged;
        
        public event Func<BoundingBox, int, int, System.Threading.CancellationToken, Task> OnDownloadStarted;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_CHILD = 5;

        public Func<Task> CustomHomeAction { get; set; }

        public MapHostPanel()
        {
            _client = new MapsuiHostClient();
            _client.MapClicked += (s, e) => 
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        if (e.Button == "Right" && this.ContextMenuStrip != null)
                        {
                            this.ContextMenuStrip.Show(this, new System.Drawing.Point((int)e.ScreenX, (int)e.ScreenY));
                        }
                    });
                }
                MapClicked?.Invoke(this, e);
            };
            _client.FeatureClicked += (s, e) => FeatureClicked?.Invoke(this, e);
            _client.ViewportChanged += (s, e) => 
            {
                CurrentZoom = e.ZoomLevel;
                ViewportChanged?.Invoke(this, e);
            };
            _client.AreaSelected += (s, e) => 
            {
                if (!IsHandleCreated || IsDisposed) return;
                Invoke((MethodInvoker)delegate
                {
                    ShowDownloadOverlay(e);
                });
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

        public async Task<string> AddPolygonAsync(string layer, double[][] coordinates, string fillColor = "#800000FF", string outlineColor = "#FF0000FF", double outlineWidth = 2)
        {
            await WhenReadyAsync();
            return await _client.AddPolygonAsync(layer, coordinates, fillColor, outlineColor, outlineWidth);
        }

        public async Task<string> AddPointAsync(string layer, double lat, double lon, string label = null, string color = "#FFFF0000", double scale = 1.0)
        {
            await WhenReadyAsync();
            return await _client.AddPointAsync(layer, lat, lon, label, color, scale);
        }

        public async Task<string> AddLineAsync(string layer, double[][] coordinates, string color = "#FF0000FF", double width = 2)
        {
            await WhenReadyAsync();
            return await _client.AddLineAsync(layer, coordinates, color, width);
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
    }
}
