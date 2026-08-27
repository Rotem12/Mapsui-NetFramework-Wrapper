namespace Mapsui48.Client
{
    /// <summary>
    /// Standard map provider tile URL templates and identifiers for use with MapHostPanel.
    /// </summary>
    public static class MapProviders
    {
        public const string OpenStreetMap = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        public const string GovmapHebrew = "https://cdnil.govmap.gov.il/xyz/heb/{z}/{x}/{y}.png";
        public const string GovmapEnglish = "https://cdnil.govmap.gov.il/xyz/eng/{z}/{x}/{y}.png";
        public const string IsraelHikingHebrew = "https://israelhiking.osm.org.il/Hebrew/Tiles/{z}/{x}/{y}.png";
        public const string IsraelHikingEnglish = "https://israelhiking.osm.org.il/English/Tiles/{z}/{x}/{y}.png";
        public const string GoogleRoadmap = "https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}";
        public const string GoogleSatellite = "https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}";
        public const string GoogleHybrid = "https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}";
        public const string GoogleTerrain = "https://mt1.google.com/vt/lyrs=p&x={x}&y={y}&z={z}";
        public const string BingAerial = "Known:BingAerial";
        public const string BingHybrid = "Known:BingHybrid";
        public const string BingRoads = "Known:BingRoads";
        public const string CartoLight = "https://cartodb-basemaps-a.global.ssl.fastly.net/light_all/{z}/{x}/{y}.png";
        public const string CartoDark = "https://cartodb-basemaps-a.global.ssl.fastly.net/dark_all/{z}/{x}/{y}.png";
        public const string EsriWorldStreet = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}";
        public const string EsriWorldSatellite = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}";
        public const string EsriWorldTopo = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}";
    }
}
