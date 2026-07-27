namespace Mapsui48.Protocol
{
    public class Coordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public Coordinate() { }

        public Coordinate(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }
    }
}
