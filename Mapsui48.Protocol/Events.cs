namespace Mapsui48.Protocol
{
    public class MapEvent
    {
        public string Type { get; set; } = "Event";
        public string EventType { get; set; }
    }

    public class MapClickedEvent : MapEvent
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ScreenX { get; set; }
        public double ScreenY { get; set; }
        public string Button { get; set; } = "Left";

        public MapClickedEvent()
        {
            EventType = "MapClicked";
        }
    }

    public class FeatureClickedEvent : MapEvent
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public FeatureClickedEvent()
        {
            EventType = "FeatureClicked";
        }
    }

    public class ViewportChangedEvent : MapEvent
    {
        public double CenterLat { get; set; }
        public double CenterLon { get; set; }
        public double ZoomLevel { get; set; }
        public double Rotation { get; set; }

        public ViewportChangedEvent()
        {
            EventType = "ViewportChanged";
        }
    }

    public class AreaSelectedEvent : MapEvent
    {
        public double MinLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLat { get; set; }
        public double MaxLon { get; set; }
        
        // Screen bounds of the selected rectangle
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }

        public AreaSelectedEvent()
        {
            EventType = "AreaSelected";
        }
    }

    public class MapPointerMovedEvent : MapEvent
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ScreenX { get; set; }
        public double ScreenY { get; set; }

        public MapPointerMovedEvent()
        {
            EventType = "MapPointerMoved";
        }
    }

    public class MapDoubleClickedEvent : MapEvent
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ScreenX { get; set; }
        public double ScreenY { get; set; }

        public MapDoubleClickedEvent()
        {
            EventType = "MapDoubleClicked";
        }
    }
}

