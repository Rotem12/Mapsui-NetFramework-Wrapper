using Mapsui.Projections;

namespace Mapsui48.Host.Helpers
{
    public static class CoordinateHelper
    {
        public static (double x, double y) ToMercator(double lat, double lon)
        {
            return SphericalMercator.FromLonLat(lon, lat);
        }

        public static (double lat, double lon) ToWgs84(double x, double y)
        {
            var lonLat = SphericalMercator.ToLonLat(x, y);
            return (lonLat.lat, lonLat.lon);
        }

        public static double ZoomLevelToResolution(double zoomLevel)
        {
            return 156543.03392804097 / System.Math.Pow(2, zoomLevel);
        }

        public static double ResolutionToZoomLevel(double resolution)
        {
            if (resolution <= 0) return 0;
            return System.Math.Log(156543.03392804097 / resolution, 2);
        }
    }
}
