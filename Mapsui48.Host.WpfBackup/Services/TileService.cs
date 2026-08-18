using BruTile;
using BruTile.Cache;
using BruTile.MbTiles;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Tiling.Layers;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mapsui48.Host.Services
{
    public static class TileService
    {
        // Cache the parsed GeoJSON provider so we don't re-read and re-parse the
        // multi-MB file every time SetTileSource is called.
        private static Mapsui.Nts.Providers.GeoJsonProvider? _countriesProvider;
        private static readonly object _providerLock = new();

        public static List<ILayer> CreateTileLayers(string mbTilesPath, string onlineUrl, string cachePath)
        {
            var layers = new List<ILayer>();

            // ── Layer 0: Offline land polygons (bottom-most) ────────────────
            // Provides tan land shapes over the sea-blue BackColor when offline.
            var landLayer = CreateLandLayer();
            if (landLayer is not null)
                layers.Add(landLayer);

            // ── Layer 1: Online OSM tiles (middle) ──────────────────────────
            if (!string.IsNullOrEmpty(onlineUrl))
            {
                IPersistentCache<byte[]>? cache = null;
                if (!string.IsNullOrEmpty(cachePath))
                {
                    // Prevent tile cache collisions by isolating each provider into its own sub-folder
                    // Use a deterministic hash (SHA256) instead of GetHashCode() which is randomized in .NET Core
                    using var sha256 = System.Security.Cryptography.SHA256.Create();
                    var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(onlineUrl));
                    var providerFolder = BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);

                    var uniqueCachePath = Path.Combine(cachePath, "MapsuiTileCache", providerFolder);
                    Directory.CreateDirectory(uniqueCachePath);
                    
                    cache = new FileCache(
                        uniqueCachePath,
                        "png",
                        TimeSpan.FromDays(30));
                }

                ITileSource onlineSource;

                if (onlineUrl.StartsWith("Known:"))
                {
                    var sourceName = onlineUrl.Substring(6);
                    if (Enum.TryParse<KnownTileSource>(sourceName, out var knownSource))
                    {
                        // Use a public Mapsui dummy API key for Bing if required (BruTile allows this for testing)
                        onlineSource = KnownTileSources.Create(knownSource, "Ar3uV_38y8b9O3W8Fh7yOa04a62zE-F43UqZ7bQY-72qD3a8e-5b12D86f_8u22C", persistentCache: cache);
                    }
                    else
                    {
                        throw new Exception($"Unknown KnownTileSource: {sourceName}");
                    }
                }
                else
                {
                    onlineSource = new HttpTileSource(
                        new GlobalSphericalMercator(),
                        onlineUrl,
                        new[] { "a", "b", "c" },
                        name: "OnlineFallback",
                        persistentCache: cache);
                }

                layers.Add(new TileLayer(onlineSource) { Name = "BaseMap_Online" });
            }

            // ── Layer 2: Transparent MBTiles overlay (top) ──────────────────
            if (!string.IsNullOrEmpty(mbTilesPath) && File.Exists(mbTilesPath))
            {
                try
                {
                    var mbtSource = new MbTilesTileSource(
                        new SQLiteConnectionString(mbTilesPath, false));
                    var transparentSource = new TransparentMbTilesTileSource(mbtSource);
                    layers.Add(new TileLayer(transparentSource) { Name = "BaseMap_Offline" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load MBTiles: {ex.Message}");
                }
            }

            // ── Fallback: plain OSM if nothing else was configured ──────────
            if (layers.Count == 0)
            {
                var defaultSource = new HttpTileSource(
                    new GlobalSphericalMercator(),
                    "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                    name: "DefaultOSM");
                layers.Add(new TileLayer(defaultSource) { Name = "BaseMap_Default" });
            }

            return layers;
        }

        /// <summary>
        /// Creates a vector layer with world country polygons filled in the OSM
        /// Carto land colour (#F2EFE9). Returns null if the asset is missing.
        /// The GeoJSON is parsed only once and then reused.
        /// </summary>
        private static Layer? CreateLandLayer()
        {
            try
            {
                var provider = GetOrCreateCountriesProvider();
                if (provider is null) return null;

                var projecting = new ProjectingProvider(provider)
                {
                    CRS = "EPSG:3857"
                };

                return new Layer("BaseMap_Land")
                {
                    DataSource = projecting,
                    Style = new Mapsui.Styles.VectorStyle
                    {
                        Fill = new Mapsui.Styles.Brush(
                            Mapsui.Styles.Color.FromString("#F2EFE9")),
                        Outline = null
                    },
                    // Optimization #3: Only draw the world continents when zoomed out (Zoom levels 0 to 8).
                    // Resolution 8 is ~611 units per pixel. So any resolution below 600 (Zoom 9+) won't draw this layer.
                    MinVisible = 600
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load GeoJSON: {ex.Message}");
                return null;
            }
        }

        private static Mapsui.Nts.Providers.GeoJsonProvider? GetOrCreateCountriesProvider()
        {
            if (_countriesProvider is not null)
                return _countriesProvider;

            lock (_providerLock)
            {
                // Double-check inside the lock
                if (_countriesProvider is not null)
                    return _countriesProvider;

                // Optimization #4: Read from embedded resource
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("Mapsui48.Host.Assets.countries.geojson");
                if (stream == null) return null;

                using var reader = new StreamReader(stream);
                string geojson = reader.ReadToEnd();
                
                _countriesProvider = new Mapsui.Nts.Providers.GeoJsonProvider(geojson);
                return _countriesProvider;
            }
        }
    }
}
