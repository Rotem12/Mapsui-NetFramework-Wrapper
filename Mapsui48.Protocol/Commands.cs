using System;

namespace Mapsui48.Protocol
{
    public class MapCommand
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public MapCommand() { }

        public MapCommand(string type)
        {
            Id = Guid.NewGuid().ToString("N");
            Type = type;
        }
    }

    public class AddPolygonCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public Coordinate[] Coordinates { get; set; }
        public string FillColor { get; set; }
        public string OutlineColor { get; set; }
        public double OutlineWidth { get; set; }

        public AddPolygonCommand() : base("AddPolygon") { }
    }

    public class AddPointCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Label { get; set; }
        public string Color { get; set; }
        public double Scale { get; set; }

        public AddPointCommand() : base("AddPoint") { }
    }

    public class AddLineCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public Coordinate[] Coordinates { get; set; }
        public string Color { get; set; }
        public double Width { get; set; }

        public AddLineCommand() : base("AddLine") { }
    }

    public class RemoveFeatureCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }

        public RemoveFeatureCommand() : base("RemoveFeature") { }
    }

    public class ClearLayerCommand : MapCommand
    {
        public string LayerName { get; set; }

        public ClearLayerCommand() : base("ClearLayer") { }
    }

    public class NavigateToCommand : MapCommand
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public NavigateToCommand() : base("NavigateTo") { }
    }

    public class FlyToCommand : MapCommand
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public FlyToCommand() : base("FlyTo") { }
    }

    public class SetZoomCommand : MapCommand
    {
        public double ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public SetZoomCommand() : base("SetZoom") { }
    }

    public class GoHomeCommand : MapCommand
    {
        public int? DurationMs { get; set; }

        public GoHomeCommand() : base("GoHome") { }
    }

    public class SetTileSourceCommand : MapCommand
    {
        public string MBTilesPath { get; set; }
        public string OnlineUrl { get; set; }
        public string CachePath { get; set; }

        public SetTileSourceCommand() : base("SetTileSource") { }
    }

    public class AttachToCommand : MapCommand
    {
        public long ParentHwnd { get; set; }

        public AttachToCommand() : base("AttachTo") { }
    }

    public class PingCommand : MapCommand
    {
        public PingCommand() : base("Ping") { }
    }

    public class LoadVectorTileCommand : MapCommand
    {
        public string MBTilesPath { get; set; }
        public LoadVectorTileCommand() : base("LoadVectorTile") { }
    }

    public class BeginAreaSelectionCommand : MapCommand
    {
        public BeginAreaSelectionCommand() : base("BeginAreaSelection") { }
    }
}
