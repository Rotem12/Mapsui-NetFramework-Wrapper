using BruTile;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace Mapsui48.Host.Services
{
    /// <summary>
    /// A decorator around an <see cref="ILocalTileSource"/> that strips the Maperitive sea
    /// fill color (#AAD3DF) from each tile, making those pixels fully transparent.
    /// This lets the online OSM map (or the offline GeoJSON land layer) show through.
    /// 
    /// Processed tiles are cached in memory so each unique tile is only decoded and
    /// scanned once.
    /// </summary>
    public sealed class TransparentMbTilesTileSource : ILocalTileSource
    {
        private readonly ILocalTileSource _inner;

        // In-memory LRU cache sized to handle extensive multi-zoom panning smoothly
        private readonly BruTile.Cache.MemoryCache<byte[]> _cache = new(1500, 2500);

        // Sea color from Transparent.mrules  (map-sea-color and water fill-color)
        // #AAD3DF  →  R 170, G 211, B 223
        private const byte SeaR = 170;
        private const byte SeaG = 211;
        private const byte SeaB = 223;

        // Tolerance band for anti-aliased fringe pixels around coastlines / rivers.
        // 15 is generous enough to catch JPEG artefacts without eating into similarly-
        // tinted features (e.g. light-blue admin boundaries are typically much darker).
        private const int Tolerance = 15;

        public TransparentMbTilesTileSource(ILocalTileSource inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        // ── ITileSource implementation ──────────────────────────────────────
        public ITileSchema Schema => _inner.Schema;
        public string Name => _inner.Name;
        public Attribution Attribution => _inner.Attribution;

        public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
        {
            // Fast path: already processed this tile.
            var cached = _cache.Find(tileInfo.Index);
            if (cached != null) return cached;

            byte[]? raw = await _inner.GetTileAsync(tileInfo);
            if (raw is null || raw.Length == 0)
            {
                return null;
            }

            byte[] processed = StripSeaColor(raw);
            _cache.Add(tileInfo.Index, processed);
            return processed;
        }

        // ── Pixel processing ────────────────────────────────────────────────

        /// <summary>
        /// Decodes a PNG/JPEG tile, zeroes the alpha channel of every pixel whose
        /// colour is within <see cref="Tolerance"/> of the sea colour, then
        /// re-encodes as PNG (which supports transparency).
        /// If no sea pixels are found (e.g. inland tiles), returns raw tile bytes directly.
        /// </summary>
        private static byte[] StripSeaColor(byte[] tileBytes)
        {
            using var bitmap = SKBitmap.Decode(tileBytes);
            if (bitmap is null)
                return tileBytes;

            // We need a mutable, 32-bpp bitmap so we can write individual pixels.
            // If the decoded bitmap is already BGRA/RGBA we mutate it in-place;
            // otherwise we create a BGRA copy.
            SKBitmap workBitmap;
            bool ownsWorkBitmap;

            if (bitmap.ColorType is SKColorType.Bgra8888 or SKColorType.Rgba8888)
            {
                workBitmap = bitmap;
                ownsWorkBitmap = false;     // 'bitmap' owns the memory
            }
            else
            {
                workBitmap = bitmap.Copy(SKColorType.Bgra8888);
                ownsWorkBitmap = true;
                if (workBitmap is null)
                    return tileBytes;       // copy failed – return original unchanged
            }

            try
            {
                bool modified = MakeSeaPixelsTransparent(workBitmap);
                if (!modified)
                {
                    // Performance optimization: No sea pixels were modified!
                    // Return raw bytes directly with zero re-encoding CPU overhead and zero memory allocation.
                    return tileBytes;
                }

                using var image = SKImage.FromBitmap(workBitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 80);
                return encoded.ToArray();
            }
            finally
            {
                if (ownsWorkBitmap)
                    workBitmap.Dispose();
            }
        }

        /// <summary>
        /// Walks every pixel via an unsafe pointer scan and sets alpha = 0 for
        /// pixels whose RGB falls within the tolerance band of the sea colour.
        /// Returns true if at least one pixel was made transparent.
        /// </summary>
        private static unsafe bool MakeSeaPixelsTransparent(SKBitmap bitmap)
        {
            IntPtr pixelsPtr = bitmap.GetPixels();
            if (pixelsPtr == IntPtr.Zero)
                return false;

            byte* ptr    = (byte*)pixelsPtr;
            int width    = bitmap.Width;
            int height   = bitmap.Height;
            int stride   = bitmap.RowBytes;
            int bpp      = bitmap.BytesPerPixel;        // should always be 4

            // BGRA: B=0 G=1 R=2 A=3     RGBA: R=0 G=1 B=2 A=3
            bool isBgra = bitmap.ColorType == SKColorType.Bgra8888;
            int rIdx = isBgra ? 2 : 0;
            int bIdx = isBgra ? 0 : 2;
            // gIdx = 1, aIdx = 3 for both layouts

            bool modified = false;

            for (int y = 0; y < height; y++)
            {
                byte* row = ptr + y * stride;
                for (int x = 0; x < width; x++)
                {
                    byte* px = row + x * bpp;

                    // Skip already-transparent pixels
                    if (px[3] == 0) continue;

                    int dr = px[rIdx] - SeaR;
                    int dg = px[1]    - SeaG;
                    int db = px[bIdx] - SeaB;

                    // Branchless-friendly: all three deltas within [-Tolerance, +Tolerance]
                    if (dr >= -Tolerance && dr <= Tolerance &&
                        dg >= -Tolerance && dg <= Tolerance &&
                        db >= -Tolerance && db <= Tolerance)
                    {
                        px[3] = 0;      // make transparent
                        modified = true;
                    }
                }
            }

            return modified;
        }
    }
}
