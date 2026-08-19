using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Mapsui48.Client
{
    /// <summary>
    /// Supported looping animation types for action button icons when hovered.
    /// </summary>
    public enum IconAnimationType
    {
        None = 0,
        /// <summary>
        /// Smooth breathing pulse scale and glowing aura.
        /// </summary>
        Pulse,
        /// <summary>
        /// Continuous 360-degree rotation of the icon.
        /// </summary>
        Rotate,
        /// <summary>
        /// Tactical radar beam sweep cone with fading trail.
        /// </summary>
        RadarSweep,
        /// <summary>
        /// Special 3D Isometric Elevation reticle with animated altitude wave, trajectory beam, and pulsing target.
        /// </summary>
        Elevation3D,
        /// <summary>
        /// Special Azimuth Compass dial with rotating heading arrow, degree ring, and bearing radar sweep.
        /// </summary>
        CompassBearing,
        /// <summary>
        /// Expanding tactical sonar / echo rings.
        /// </summary>
        SonarWave,
        /// <summary>
        /// Tactical target lock box with oscillating reticle brackets.
        /// </summary>
        CrosshairLock,
        /// <summary>
        /// Smooth floating up-and-down vertical bob.
        /// </summary>
        Bounce,
        /// <summary>
        /// Thermal infrared raster scanline with heat color spectrum.
        /// </summary>
        ThermalScan,
        /// <summary>
        /// Tactical ballistic trajectory arc with booster burn and impact blast wave.
        /// </summary>
        MissileTrajectory,
        /// <summary>
        /// Satellite orbiting planet/station with conical radio transmission downlink.
        /// </summary>
        OrbitSatellite,
        /// <summary>
        /// Hexagonal energy shield with harmonic barrier ripples.
        /// </summary>
        ShieldDefense,
        /// <summary>
        /// High-precision laser rangefinder with pulsing beam and distance markers.
        /// </summary>
        LaserRangefinder,
        /// <summary>
        /// High-threat tactical strobe beacon with rapid flashing hazard alert.
        /// </summary>
        StrobeAlert,
        /// <summary>
        /// Aviation artificial horizon gyro with banking roll and pitch ladder.
        /// </summary>
        FlightHorizon,
        /// <summary>
        /// Maritime vessel with dynamic bow wake ripples and rotating sonar sweep.
        /// </summary>
        MaritimeWake,
        /// <summary>
        /// Tactical rally waypoint flag with realistic cloth wave physics.
        /// </summary>
        FlagWaving,
        /// <summary>
        /// Sniper mil-dot optical scope with breathing optical zoom magnification.
        /// </summary>
        SniperScope,
        /// <summary>
        /// Thermal flame hotspot with rising combustion particle embers.
        /// </summary>
        FlamePulse,
        /// <summary>
        /// Helipad landing zone with high-speed spinning dual rotor and flashing LZ beacons.
        /// </summary>
        HelipadLZ,
        /// <summary>
        /// Custom user-defined drawing routine.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Event context passed to context menu action delegates when an action button is triggered.
    /// </summary>
    public class MapContextMenuContext
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Point ScreenPoint { get; set; }
        public Point MapPoint { get; set; }
        public MapHostPanel Map { get; set; }
        public object Tag { get; set; }

        public MapContextMenuContext(double lat, double lon, Point screenPoint, Point mapPoint, MapHostPanel map, object tag = null)
        {
            Latitude = lat;
            Longitude = lon;
            ScreenPoint = screenPoint;
            MapPoint = mapPoint;
            Map = map;
            Tag = tag;
        }
    }

    /// <summary>
    /// Represents an actionable button displayed within the Map Context Menu Overlay.
    /// </summary>
    public class MapContextMenuItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tooltip { get; set; }
        public string IconKey { get; set; }
        public Color? AccentColor { get; set; }
        public IconAnimationType AnimationType { get; set; } = IconAnimationType.Pulse;
        public float IconScale { get; set; } = 1.0f;
        public Action<Graphics, RectangleF, float, bool> CustomDrawIcon { get; set; }
        public Action<MapContextMenuContext> OnClick { get; set; }
        public Func<MapContextMenuContext, Task> OnClickAsync { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public object Tag { get; set; }

        public MapContextMenuItem()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public MapContextMenuItem(string name, string iconKey, Action<MapContextMenuContext> onClick, IconAnimationType animation = IconAnimationType.Pulse, string tooltip = null, Color? accentColor = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            IconKey = iconKey;
            OnClick = onClick;
            AnimationType = animation;
            Tooltip = tooltip ?? name;
            AccentColor = accentColor;
        }

        public MapContextMenuItem(string name, string iconKey, Func<MapContextMenuContext, Task> onClickAsync, IconAnimationType animation = IconAnimationType.Pulse, string tooltip = null, Color? accentColor = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            IconKey = iconKey;
            OnClickAsync = onClickAsync;
            AnimationType = animation;
            Tooltip = tooltip ?? name;
            AccentColor = accentColor;
        }
    }
}
