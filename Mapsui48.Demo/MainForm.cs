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
            
            // Hook live PointerMoved event for the Custom Tooltip Test
            _mapPanel.PointerMoved += MapPanel_PointerMoved;

            panelMapContainer.Controls.Add(_mapPanel);

            InitializeProviderDropdown();
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
