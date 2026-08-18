using Mapsui;
using Mapsui.Styles;
using Mapsui.Providers;
using System;
using System.Collections.Generic;

namespace Mapsui48.Host.Services
{
    public static class OpenMapTilesStyler
    {
        private static int _debugCount = 0;
        private static readonly Mapsui.Styles.Pen OutlinePen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Gray, 1);
        private static readonly Mapsui.Styles.Brush BuildingBrush = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#EAEADD"));
        private static readonly Mapsui.Styles.Pen BuildingPen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromString("#D5D5D5"), 1);
        
        private static readonly Mapsui.Styles.Brush WaterBrush = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#AAD3DF"));
        private static readonly Mapsui.Styles.Brush LanduseBrush = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#D8F2C6")); // Park green
        
        private static readonly Mapsui.Styles.Pen MotorwayPen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromString("#E892A2"), 4);
        private static readonly Mapsui.Styles.Pen PrimaryPen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromString("#FCD6A4"), 3);
        private static readonly Mapsui.Styles.Pen MinorRoadPen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 2);
        private static readonly Mapsui.Styles.Pen BoundaryPen = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Purple, 2) { PenStyle = PenStyle.Dash };

        public static IStyle GetStyle(IFeature feature)
        {
            var layerName = feature["layer"]?.ToString() ?? "";
            var cls = feature["class"]?.ToString() ?? "";
            
            // Debug dump
            try {
                if (_debugCount < 100) {
                    _debugCount++;
                    var tags = new List<string>();
                    foreach(var field in feature.Fields) tags.Add(field + "=" + feature[field]);
                    
                    var geomType = (feature is Mapsui.Nts.GeometryFeature gf && gf.Geometry != null) ? gf.Geometry.GeometryType : "Unknown";
                    System.IO.File.AppendAllText("mvt_debug.txt", $"FeatureType={feature.GetType().Name}, Geom={geomType}, Layer={layerName}, Class={cls}, Fields: {string.Join(", ", tags)}\n");
                }
            } catch {}

            switch (layerName)
            {
                case "water":
                case "waterway":
                    var waterColor = WaterBrush.Color ?? Mapsui.Styles.Color.Transparent;
                    return new VectorStyle
                    {
                        Fill = WaterBrush,
                        Outline = new Mapsui.Styles.Pen(waterColor, 1),
                        Line = new Mapsui.Styles.Pen(waterColor, 2)
                    };

                case "landcover":
                case "landuse":
                case "park":
                    return new VectorStyle
                    {
                        Fill = LanduseBrush,
                        Outline = null
                    };

                case "building":
                    return new VectorStyle
                    {
                        Fill = BuildingBrush,
                        Outline = BuildingPen
                    };

                case "transportation":
                    Mapsui.Styles.Pen roadPen = MinorRoadPen;
                    if (cls == "motorway" || cls == "trunk") roadPen = MotorwayPen;
                    else if (cls == "primary" || cls == "secondary") roadPen = PrimaryPen;

                    return new VectorStyle
                    {
                        Line = roadPen,
                        Outline = OutlinePen
                    };

                case "boundary":
                    return new VectorStyle
                    {
                        Line = BoundaryPen
                    };

                default:
                    // Transparent for anything else
                    return new VectorStyle { Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Transparent), Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Transparent) };
            }
        }
    }
}
