namespace Mapsui48.Protocol
{
    public class MapResponse
    {
        public string Id { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Data { get; set; }
    }

    public class LayerInfoDto
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public double Opacity { get; set; }
        public double MinVisible { get; set; }
        public double MaxVisible { get; set; }
        public int FeatureCount { get; set; }
    }

    public class CoordinateResultDto

    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ScreenX { get; set; }
        public double ScreenY { get; set; }
    }

    public class BoundsResultDto
    {
        public double MinLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLat { get; set; }
        public double MaxLon { get; set; }
    }
}


