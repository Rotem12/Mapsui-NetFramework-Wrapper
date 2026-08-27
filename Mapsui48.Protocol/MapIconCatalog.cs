using System;
using System.Collections.Generic;
using System.Linq;

namespace Mapsui48.Protocol
{
    public class IconDefinition
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string[] Aliases { get; set; }
        public string SvgTemplate { get; set; }

        public IconDefinition(string key, string name, string category, string[] aliases, string svgTemplate)
        {
            Key = key;
            Name = name;
            Category = category;
            Aliases = aliases ?? Array.Empty<string>();
            SvgTemplate = svgTemplate;
        }
    }

    public class ColorDefinition
    {
        public string Name { get; set; }
        public string Hex { get; set; }

        public ColorDefinition(string name, string hex)
        {
            Name = name;
            Hex = hex;
        }
    }

    /// <summary>
    /// Central registry of tactical & GIS icons, color schemes, and dynamic SVG color substitution.
    /// </summary>
    public static class MapIconCatalog
    {
        public static readonly List<ColorDefinition> StandardColors = new List<ColorDefinition>
        {
            new ColorDefinition("Red", "#EF4444"),
            new ColorDefinition("Cyan", "#00E5FF"),
            new ColorDefinition("Green", "#22C55E"),
            new ColorDefinition("Amber / Orange", "#F59E0B"),
            new ColorDefinition("Purple", "#A855F7"),
            new ColorDefinition("Blue", "#3B82F6"),
            new ColorDefinition("White", "#F8FAFC"),
            new ColorDefinition("Yellow", "#EAB308"),
            new ColorDefinition("Pink", "#EC4899"),
            new ColorDefinition("Lime", "#84CC16"),
            new ColorDefinition("Dark Gray", "#475569")
        };

        private static readonly Dictionary<string, IconDefinition> _iconsByKey = new Dictionary<string, IconDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _aliasToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static MapIconCatalog()
        {
            RegisterAllIcons();
        }

        public static IReadOnlyList<IconDefinition> GetAllIcons()
        {
            return _iconsByKey.Values.ToList();
        }

        public static IEnumerable<IGrouping<string, IconDefinition>> GetIconsByCategory()
        {
            return _iconsByKey.Values.GroupBy(i => i.Category);
        }

        public static bool TryGetIcon(string keyOrAlias, out IconDefinition icon)
        {
            if (string.IsNullOrEmpty(keyOrAlias))
            {
                icon = null;
                return false;
            }

            if (_iconsByKey.TryGetValue(keyOrAlias, out icon))
                return true;

            if (_aliasToKey.TryGetValue(keyOrAlias, out var realKey) && _iconsByKey.TryGetValue(realKey, out icon))
                return true;

            icon = null;
            return false;
        }

        public static string ResolveIconKey(string keyOrAlias, string layerName = null)
        {
            if (string.IsNullOrEmpty(keyOrAlias))
            {
                if (string.Equals(layerName, "Camera", StringComparison.OrdinalIgnoreCase))
                    return "camera_ptz";
                if (string.Equals(layerName, "Targets", StringComparison.OrdinalIgnoreCase))
                    return "crosshair";
                return null;
            }

            if (_iconsByKey.ContainsKey(keyOrAlias))
                return keyOrAlias;

            if (_aliasToKey.TryGetValue(keyOrAlias, out var realKey))
                return realKey;

            return keyOrAlias;
        }

        /// <summary>
        /// Generates a fully colorized SVG string for the specified icon and color.
        /// </summary>
        public static string GetColorizedSvg(string keyOrAlias, string hexColor, string instanceId = "0")
        {
            if (!TryGetIcon(keyOrAlias, out var icon))
                return null;

            if (string.IsNullOrEmpty(hexColor))
                hexColor = "#00E5FF";

            ParseHexColor(hexColor, out int r, out int g, out int b);

            int rLight = Math.Min(255, r + 55);
            int gLight = Math.Min(255, g + 55);
            int bLight = Math.Min(255, b + 55);

            int rDark = Math.Max(0, r - 70);
            int gDark = Math.Max(0, g - 70);
            int bDark = Math.Max(0, b - 70);

            string hexLight = $"#{rLight:X2}{gLight:X2}{bLight:X2}";
            string hexDark = $"#{rDark:X2}{gDark:X2}{bDark:X2}";

            return icon.SvgTemplate
                .Replace("{ID}", instanceId)
                .Replace("{COLOR}", hexColor)
                .Replace("{COLOR_LIGHT}", hexLight)
                .Replace("{COLOR_DARK}", hexDark);
        }

        private static void ParseHexColor(string hex, out int r, out int g, out int b)
        {
            r = 0; g = 229; b = 255;
            if (string.IsNullOrEmpty(hex)) return;

            string clean = hex.TrimStart('#');
            if (clean.Length == 8) clean = clean.Substring(2);
            if (clean.Length == 6)
            {
                if (int.TryParse(clean.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int red) &&
                    int.TryParse(clean.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int green) &&
                    int.TryParse(clean.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int blue))
                {
                    r = red;
                    g = green;
                    b = blue;
                }
            }
            else if (clean.Length == 3)
            {
                if (int.TryParse(new string(clean[0], 2), System.Globalization.NumberStyles.HexNumber, null, out int red) &&
                    int.TryParse(new string(clean[1], 2), System.Globalization.NumberStyles.HexNumber, null, out int green) &&
                    int.TryParse(new string(clean[2], 2), System.Globalization.NumberStyles.HexNumber, null, out int blue))
                {
                    r = red;
                    g = green;
                    b = blue;
                }
            }
        }

        private static void Register(string key, string name, string category, string[] aliases, string svgTemplate)
        {
            var def = new IconDefinition(key, name, category, aliases, svgTemplate);
            _iconsByKey[key] = def;
            if (aliases != null)
            {
                foreach (var alias in aliases)
                {
                    _aliasToKey[alias] = key;
                }
            }
        }

        private static void RegisterAllIcons()
        {
            // =============================================================
            // CATEGORY 1: CAMERAS & SENSORS
            // =============================================================
            Register("camera_ptz", "Tactical PTZ", "Cameras & Sensors", new[] { "camera", "ptz" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <defs><linearGradient id=""lensG_{ID}"" x1=""0%"" y1=""100%"" x2=""0%"" y2=""0%""><stop offset=""0%"" stop-color=""{COLOR_DARK}""/><stop offset=""100%"" stop-color=""{COLOR_LIGHT}""/></linearGradient></defs>
  <circle cx=""0"" cy=""0"" r=""12.5"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -6.5 -3 L 0 -13 L 6.5 -3 Z"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <circle cx=""0"" cy=""0"" r=""9"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.5""/>
  <circle cx=""0"" cy=""0"" r=""6"" fill=""#0f172a"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""0"" cy=""-0.5"" r=""3.2"" fill=""url(#lensG_{ID})""/>
  <circle cx=""-0.8"" cy=""-1.3"" r=""0.9"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""6.2"" r=""1.2"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""0.5""/>
</svg>");

            Register("camera_cctv", "Bullet CCTV", "Cameras & Sensors", new[] { "cctv", "bullet" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <defs><linearGradient id=""lensG_{ID}"" x1=""0%"" y1=""100%"" x2=""0%"" y2=""0%""><stop offset=""0%"" stop-color=""{COLOR_DARK}""/><stop offset=""100%"" stop-color=""{COLOR_LIGHT}""/></linearGradient></defs>
  <path d=""M -6.5 -11.5 L 6.5 -11.5 L 5.5 8 L -5.5 8 Z"" fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2"" stroke-linejoin=""round""/>
  <rect x=""-3.5"" y=""6.5"" width=""7"" height=""4"" rx=""1"" fill=""#475569"" stroke=""#0f172a"" stroke-width=""1""/>
  <path d=""M -4.5 -6 L 4.5 -6 L 4 7 L -4 7 Z"" fill=""#ffffff"" stroke=""#0f172a"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <path d=""M -6 -5 L -6.5 -11 L 6.5 -11 L 6 -5 Z"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <ellipse cx=""0"" cy=""-10"" rx=""4.5"" ry=""1.6"" fill=""url(#lensG_{ID})""/>
  <circle cx=""0"" cy=""-10"" r=""1"" fill=""#ffffff""/>
  <circle cx=""2.5"" cy=""-2"" r=""0.9"" fill=""{COLOR}""/>
</svg>");

            Register("camera_dome", "PTZ Dome", "Cameras & Sensors", new[] { "dome" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-13 -13 26 26"" width=""26"" height=""26"">
  <defs><linearGradient id=""lensG_{ID}"" x1=""0%"" y1=""100%"" x2=""0%"" y2=""0%""><stop offset=""0%"" stop-color=""{COLOR_DARK}""/><stop offset=""100%"" stop-color=""{COLOR_LIGHT}""/></linearGradient></defs>
  <circle cx=""0"" cy=""0"" r=""11.5"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -5 -2 L 0 -11.5 L 5 -2 Z"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.3"" stroke-linejoin=""round""/>
  <circle cx=""0"" cy=""0"" r=""8.5"" fill=""#ffffff"" stroke=""#0f172a"" stroke-width=""1.5""/>
  <circle cx=""0"" cy=""-0.5"" r=""5.5"" fill=""#0f172a""/>
  <circle cx=""0"" cy=""-1"" r=""3"" fill=""url(#lensG_{ID})""/>
  <circle cx=""-0.7"" cy=""-1.7"" r=""0.8"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""5"" r=""1"" fill=""{COLOR}""/>
</svg>");

            Register("camera_thermal", "Thermal IR", "Cameras & Sensors", new[] { "thermal", "flir" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <defs><linearGradient id=""lensG_{ID}"" x1=""0%"" y1=""100%"" x2=""0%"" y2=""0%""><stop offset=""0%"" stop-color=""{COLOR_DARK}""/><stop offset=""100%"" stop-color=""{COLOR_LIGHT}""/></linearGradient></defs>
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""-6,-3 0,-12.5 6,-3"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.3""/>
  <rect x=""-8"" y=""-6"" width=""16"" height=""14"" rx=""3"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <circle cx=""-3.5"" cy=""1"" r=""2.8"" fill=""url(#lensG_{ID})"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <circle cx=""3.5"" cy=""1"" r=""2.8"" fill=""#0f172a"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""3.5"" cy=""1"" r=""1.2"" fill=""{COLOR}""/>
  <circle cx=""-4"" cy=""0.2"" r=""0.7"" fill=""#ffffff""/>
</svg>");

            Register("radar", "Radar Dish", "Cameras & Sensors", new[] { "antenna" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <path d=""M 0 0 L -6 -8 A 10 10 0 0 1 6 -8 Z"" fill=""{COLOR}"" fill-opacity=""0.6"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""0"" cy=""0"" r=""6.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""0.8"" stroke-dasharray=""2,2""/>
  <circle cx=""0"" cy=""0"" r=""3"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""0"" cy=""0"" r=""1.2"" fill=""#ffffff""/>
</svg>");

            Register("tower", "Tower Mast", "Cameras & Sensors", new[] { "mast", "base_station" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <path d=""M -6 -4 A 8 8 0 0 1 6 -4"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <path d=""M -4 -1 A 5 5 0 0 1 4 -1"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <polygon points=""-2,7 2,7 1,2 -1,2"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""1"" r=""2"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""0.8""/>
</svg>");

            Register("sensor_motion", "Motion Sensor", "Cameras & Sensors", new[] { "motion", "pir", "seismic" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""-8,8 8,8 0,-8"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <path d=""M -5 -2 A 7 7 0 0 1 5 -2"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <path d=""M -3 1 A 4 4 0 0 1 3 1"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""4.5"" r=""1.5"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.7""/>
</svg>");

            Register("searchlight", "Searchlight", "Cameras & Sensors", new[] { "spotlight", "illuminator" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""0,0 -8,-12 8,-12"" fill=""{COLOR}"" fill-opacity=""0.4"" stroke=""{COLOR}"" stroke-width=""1"" stroke-dasharray=""2,2""/>
  <circle cx=""0"" cy=""2"" r=""6"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""2"" r=""3"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
</svg>");

            // =============================================================
            // CATEGORY 2: TARGETS & RETICLES
            // =============================================================
            Register("crosshair", "Crosshair", "Targets & Reticles", new[] { "target", "reticle" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""8.5"" fill=""none"" stroke=""#ffffff"" stroke-width=""2.5""/>
  <circle cx=""0"" cy=""0"" r=""8.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.6""/>
  <line x1=""0"" y1=""-12"" x2=""0"" y2=""-5"" stroke=""{COLOR}"" stroke-width=""1.8"" stroke-linecap=""round""/>
  <line x1=""0"" y1=""5"" x2=""0"" y2=""12"" stroke=""{COLOR}"" stroke-width=""1.8"" stroke-linecap=""round""/>
  <line x1=""-12"" y1=""0"" x2=""-5"" y2=""0"" stroke=""{COLOR}"" stroke-width=""1.8"" stroke-linecap=""round""/>
  <line x1=""5"" y1=""0"" x2=""12"" y2=""0"" stroke=""{COLOR}"" stroke-width=""1.8"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""0"" r=""1.8"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
</svg>");

            Register("bullseye", "Bullseye", "Targets & Reticles", new[] { "rings" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""none"" stroke=""#ffffff"" stroke-width=""2.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.6""/>
  <circle cx=""0"" cy=""0"" r=""5.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.3""/>
  <circle cx=""0"" cy=""0"" r=""2.5"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
</svg>");

            Register("diamond_target", "Diamond Target", "Targets & Reticles", new[] { "diamond", "diamond_reticle" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M 0 -13 L 13 0 L 0 13 L -13 0 Z"" fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2""/>
  <path d=""M 0 -10.5 L 10.5 0 L 0 10.5 L -10.5 0 Z"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.5""/>
  <path d=""M 0 -7 L 7 0 L 0 7 L -7 0 Z"" fill=""{COLOR}"" fill-opacity=""0.3"" stroke=""{COLOR}"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""0"" r=""2.2"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
</svg>");

            Register("tracking_box", "Lock Box", "Targets & Reticles", new[] { "lock_box", "box", "tracker" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <rect x=""-11"" y=""-11"" width=""22"" height=""22"" rx=""2"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -9 -4 L -9 -9 L -4 -9"" fill=""none"" stroke=""{COLOR}"" stroke-width=""2"" stroke-linecap=""square""/>
  <path d=""M 4 -9 L 9 -9 L 9 -4"" fill=""none"" stroke=""{COLOR}"" stroke-width=""2"" stroke-linecap=""square""/>
  <path d=""M -9 4 L -9 9 L -4 9"" fill=""none"" stroke=""{COLOR}"" stroke-width=""2"" stroke-linecap=""square""/>
  <path d=""M 4 9 L 9 9 L 9 4"" fill=""none"" stroke=""{COLOR}"" stroke-width=""2"" stroke-linecap=""square""/>
  <line x1=""-2.5"" y1=""0"" x2=""2.5"" y2=""0"" stroke=""#ffffff"" stroke-width=""1.2""/>
  <line x1=""0"" y1=""-2.5"" x2=""0"" y2=""2.5"" stroke=""#ffffff"" stroke-width=""1.2""/>
</svg>");

            Register("threat", "Threat Warning", "Targets & Reticles", new[] { "warning_target", "danger" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M 0 -13 L 13 10 L -13 10 Z"" fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2""/>
  <path d=""M 0 -11 L 11 8.5 L -11 8.5 Z"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.5"" stroke-linejoin=""round""/>
  <path d=""M 0 -8 L 8 6 L -8 6 Z"" fill=""{COLOR}"" stroke=""{COLOR}"" stroke-width=""1"" stroke-linejoin=""round""/>
  <line x1=""0"" y1=""-3.5"" x2=""0"" y2=""1"" stroke=""#ffffff"" stroke-width=""1.8"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""3.8"" r=""1"" fill=""#ffffff""/>
</svg>");

            Register("hostile", "Hostile Unit", "Targets & Reticles", new[] { "enemy", "bandit" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M -11 -9 L 11 -9 L 0 11 Z"" fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2""/>
  <path d=""M -9 -7.5 L 9 -7.5 L 0 8.5 Z"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.5"" stroke-linejoin=""round""/>
  <polygon points=""-5.5,-5 5.5,-5 0,4"" fill=""{COLOR}""/>
  <circle cx=""0"" cy=""-2"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("sniper", "Sniper Reticle", "Targets & Reticles", new[] { "marksman", "overwatch" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <line x1=""0"" y1=""-9"" x2=""0"" y2=""9"" stroke=""{COLOR}"" stroke-width=""1.2""/>
  <line x1=""-9"" y1=""0"" x2=""9"" y2=""0"" stroke=""{COLOR}"" stroke-width=""1.2""/>
  <circle cx=""0"" cy=""-4"" r=""0.7"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""4"" r=""0.7"" fill=""#ffffff""/>
  <circle cx=""-4"" cy=""0"" r=""0.7"" fill=""#ffffff""/>
  <circle cx=""4"" cy=""0"" r=""0.7"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""0"" r=""1.5"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.6""/>
</svg>");

            // =============================================================
            // CATEGORY 3: DEFENSE, VEHICLES & TACTICAL UNITS
            // =============================================================
            Register("drone", "Drone UAV", "Defense & Vehicles", new[] { "uav", "quadcopter" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <line x1=""-8"" y1=""-8"" x2=""8"" y2=""8"" stroke=""#ffffff"" stroke-width=""2.5"" stroke-linecap=""round""/>
  <line x1=""-8"" y1=""8"" x2=""8"" y2=""-8"" stroke=""#ffffff"" stroke-width=""2.5"" stroke-linecap=""round""/>
  <line x1=""-8"" y1=""-8"" x2=""8"" y2=""8"" stroke=""#0f172a"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <line x1=""-8"" y1=""8"" x2=""8"" y2=""-8"" stroke=""#0f172a"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <circle cx=""-8"" cy=""-8"" r=""3"" fill=""{COLOR}"" fill-opacity=""0.4"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""8"" cy=""-8"" r=""3"" fill=""{COLOR}"" fill-opacity=""0.4"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""-8"" cy=""8"" r=""3"" fill=""{COLOR}"" fill-opacity=""0.4"" stroke=""{COLOR}"" stroke-width=""1""/>
  <circle cx=""8"" cy=""8"" r=""3"" fill=""{COLOR}"" fill-opacity=""0.4"" stroke=""{COLOR}"" stroke-width=""1""/>
  <polygon points=""0,-6 4,3 -4,3"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1""/>
  <circle cx=""0"" cy=""0"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("aircraft", "Aircraft", "Defense & Vehicles", new[] { "plane", "jet" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M 0 -12 L 2.5 -4 L 11 0 L 11 2.5 L 2.5 1 L 2 8 L 5.5 11 L 5.5 12.5 L 0 11 L -5.5 12.5 L -5.5 11 L -2 8 L -2.5 1 L -11 2.5 L -11 0 L -2.5 -4 Z"" 
        fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.2"" stroke-linejoin=""round""/>
  <circle cx=""0"" cy=""-3"" r=""1.2"" fill=""#ffffff""/>
</svg>");

            Register("helicopter", "Helicopter", "Defense & Vehicles", new[] { "helo", "chopper" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <line x1=""0"" y1=""-11"" x2=""0"" y2=""11"" stroke=""#ffffff"" stroke-width=""2"" stroke-linecap=""round""/>
  <line x1=""-11"" y1=""0"" x2=""11"" y2=""0"" stroke=""#ffffff"" stroke-width=""2"" stroke-linecap=""round""/>
  <ellipse cx=""0"" cy=""-1"" rx=""4"" ry=""7"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.2""/>
  <line x1=""0"" y1=""6"" x2=""0"" y2=""11"" stroke=""{COLOR}"" stroke-width=""2"" stroke-linecap=""round""/>
  <line x1=""-2.5"" y1=""11"" x2=""2.5"" y2=""11"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""-1"" r=""2"" fill=""#ffffff"" stroke=""#0f172a"" stroke-width=""0.8""/>
</svg>");

            Register("vehicle", "Ground Vehicle", "Defense & Vehicles", new[] { "car", "truck", "patrol" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <rect x=""-5"" y=""-9"" width=""10"" height=""18"" rx=""3"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.5""/>
  <rect x=""-3.5"" y=""-4"" width=""7"" height=""8"" rx=""1.5"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1""/>
  <circle cx=""-3.2"" cy=""-8"" r=""1"" fill=""#ffffff""/>
  <circle cx=""3.2"" cy=""-8"" r=""1"" fill=""#ffffff""/>
  <circle cx=""-3.2"" cy=""7.5"" r=""0.8"" fill=""#ef4444""/>
  <circle cx=""3.2"" cy=""7.5"" r=""0.8"" fill=""#ef4444""/>
</svg>");

            Register("tank", "Tank Armor", "Defense & Vehicles", new[] { "armor", "afv" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <rect x=""-8"" y=""-8"" width=""3"" height=""16"" rx=""1"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1""/>
  <rect x=""5"" y=""-8"" width=""3"" height=""16"" rx=""1"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1""/>
  <rect x=""-5"" y=""-7"" width=""10"" height=""14"" rx=""1.5"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.2""/>
  <line x1=""0"" y1=""0"" x2=""0"" y2=""-12"" stroke=""#ffffff"" stroke-width=""2.2"" stroke-linecap=""round""/>
  <line x1=""0"" y1=""0"" x2=""0"" y2=""-12"" stroke=""{COLOR}"" stroke-width=""1.2"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""0"" r=""3.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1""/>
</svg>");

            Register("missile", "Air Defense", "Defense & Vehicles", new[] { "sam", "launcher", "rocket" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -3 -11 L 0 -13 L 3 -11 L 2 7 L -2 7 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.2"" stroke-linejoin=""round""/>
  <polygon points=""-5,8 -2,4 -2,8"" fill=""#ffffff""/>
  <polygon points=""5,8 2,4 2,8"" fill=""#ffffff""/>
  <circle cx=""0"" cy=""0"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("vessel", "Maritime Vessel", "Defense & Vehicles", new[] { "ship", "boat" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M 0 -11 L 6 -1 L 5 9 L -5 9 L -6 -1 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <rect x=""-2.5"" y=""0"" width=""5"" height=""5"" rx=""1"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <circle cx=""0"" cy=""-4"" r=""1"" fill=""#ffffff""/>
</svg>");

            Register("submarine", "Submarine", "Defense & Vehicles", new[] { "sub", "sonar" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M 0 -11 C 3.5 -11, 3.5 10, 0 11 C -3.5 10, -3.5 -11, 0 -11 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <ellipse cx=""0"" cy=""-1"" rx=""1.6"" ry=""4"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <line x1=""-6"" y1=""-1"" x2=""6"" y2=""-1"" stroke=""#ffffff"" stroke-width=""1.5"" stroke-linecap=""round""/>
</svg>");

            Register("person", "Operator Person", "Defense & Vehicles", new[] { "soldier", "infantry", "human" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""-3.5"" r=""3"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <path d=""M -6 5.5 C -6 2, 6 2, 6 5.5 Z"" fill=""{COLOR}""/>
  <polygon points=""0,-9 2.5,-6.5 -2.5,-6.5"" fill=""#ffffff""/>
</svg>");

            Register("police", "Police Security", "Defense & Vehicles", new[] { "guard", "patrol_officer" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M 0 -10 L 7 -6 L 6 3 C 6 7, 0 10, 0 10 C 0 10, -6 7, -6 3 L -7 -6 Z"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <polygon points=""0,-6 1.8,-1.5 6,-1.5 2.5,1 4,5.5 0,2.5 -4,5.5 -2.5,1 -6,-1.5 -1.8,-1.5"" fill=""{COLOR}""/>
</svg>");

            // =============================================================
            // CATEGORY 4: PERIMETER & FACILITIES
            // =============================================================
            Register("gate", "Security Gate", "Perimeter & Facilities", new[] { "checkpoint", "barrier" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <rect x=""-9"" y=""-7"" width=""3"" height=""14"" rx=""1"" fill=""#ffffff""/>
  <rect x=""6"" y=""-7"" width=""3"" height=""14"" rx=""1"" fill=""#ffffff""/>
  <line x1=""-6"" y1=""-1"" x2=""6"" y2=""-1"" stroke=""{COLOR}"" stroke-width=""3"" stroke-linecap=""round""/>
  <line x1=""-6"" y1=""-1"" x2=""6"" y2=""-1"" stroke=""#ffffff"" stroke-width=""1"" stroke-dasharray=""2,2""/>
  <circle cx=""-7.5"" cy=""-8.5"" r=""1.5"" fill=""{COLOR}""/>
</svg>");

            Register("lock", "Secure Lock", "Perimeter & Facilities", new[] { "vault", "secure_zone" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -4 0 L -4 -4 C -4 -7, 4 -7, 4 -4 L 4 0"" fill=""none"" stroke=""#ffffff"" stroke-width=""2"" stroke-linecap=""round""/>
  <rect x=""-7"" y=""-1"" width=""14"" height=""10"" rx=""2"" fill=""{COLOR}"" stroke=""#0f172a"" stroke-width=""1.2""/>
  <circle cx=""0"" cy=""3"" r=""1.5"" fill=""#ffffff""/>
  <line x1=""0"" y1=""4"" x2=""0"" y2=""6.5"" stroke=""#ffffff"" stroke-width=""1.2""/>
</svg>");

            Register("hq_bunker", "HQ Command", "Perimeter & Facilities", new[] { "hq", "c2", "command_post" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""0,-9 9,8 -9,8"" fill=""#1e293b"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <polygon points=""0,-5 5,7 -5,7"" fill=""{COLOR}""/>
  <circle cx=""0"" cy=""-2"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("helipad", "Helipad LZ", "Perimeter & Facilities", new[] { "lz", "landing_zone" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""0"" r=""7.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.2""/>
  <line x1=""-3.5"" y1=""-4.5"" x2=""-3.5"" y2=""4.5"" stroke=""#ffffff"" stroke-width=""1.8"" stroke-linecap=""square""/>
  <line x1=""3.5"" y1=""-4.5"" x2=""3.5"" y2=""4.5"" stroke=""#ffffff"" stroke-width=""1.8"" stroke-linecap=""square""/>
  <line x1=""-3.5"" y1=""0"" x2=""3.5"" y2=""0"" stroke=""#ffffff"" stroke-width=""1.8""/>
</svg>");

            Register("relay", "Comms Relay", "Perimeter & Facilities", new[] { "wifi", "mesh", "repeater" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M -7 -4 A 9 9 0 0 1 7 -4"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.6"" stroke-linecap=""round""/>
  <path d=""M -4 -1 A 5 5 0 0 1 4 -1"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.6"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""3"" r=""2"" fill=""#ffffff""/>
  <line x1=""0"" y1=""5"" x2=""0"" y2=""9"" stroke=""#ffffff"" stroke-width=""1.5""/>
</svg>");

            Register("satellite", "Satellite Uplink", "Perimeter & Facilities", new[] { "satcom", "gps" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <rect x=""-10"" y=""-3"" width=""6"" height=""6"" rx=""1"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <rect x=""4"" y=""-3"" width=""6"" height=""6"" rx=""1"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8""/>
  <rect x=""-2.5"" y=""-3.5"" width=""5"" height=""7"" rx=""1"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1""/>
  <path d=""M -3 -6 A 4 4 0 0 1 3 -6"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <circle cx=""0"" cy=""-8"" r=""1"" fill=""#ffffff""/>
</svg>");

            // =============================================================
            // CATEGORY 5: EMERGENCY, SEARCH & RESCUE & HAZMAT
            // =============================================================
            Register("medic", "Field Medic", "Emergency & HAZMAT", new[] { "hospital", "first_aid", "ambulance" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#ffffff"" stroke=""#0f172a"" stroke-width=""1.4""/>
  <path d=""M -2.5 -6.5 L 2.5 -6.5 L 2.5 -2.5 L 6.5 -2.5 L 6.5 2.5 L 2.5 2.5 L 2.5 6.5 L -2.5 6.5 L -2.5 2.5 L -6.5 2.5 L -6.5 -2.5 L -2.5 -2.5 Z"" fill=""{COLOR}""/>
</svg>");

            Register("fire", "Fire Hotspot", "Emergency & HAZMAT", new[] { "flame", "hotspot", "wildfire" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <path d=""M 0 -11 C 2 -7, 6 -4, 6 2 C 6 6, 3 8.5, 0 8.5 C -3 8.5, -6 6, -6 2 C -6 -2, -2 -6, 0 -11 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.2""/>
  <path d=""M 0 -4 C 1 -2, 3 0, 3 3 C 3 5, 1 6.5, 0 6.5 C -1 6.5, -3 5, -3 3 C -3 1, -1 -2, 0 -4 Z"" fill=""#ffffff""/>
</svg>");

            Register("distress", "Distress Beacon", "Emergency & HAZMAT", new[] { "sos", "lifebuoy", "sar" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""8.5"" fill=""#ffffff"" stroke=""#0f172a"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""0"" r=""4.5"" fill=""#0f172a""/>
  <circle cx=""0"" cy=""0"" r=""8.5"" fill=""none"" stroke=""{COLOR}"" stroke-width=""2.5"" stroke-dasharray=""5,4""/>
  <circle cx=""0"" cy=""0"" r=""2"" fill=""{COLOR}""/>
</svg>");

            Register("radiation", "HAZMAT CBRN", "Emergency & HAZMAT", new[] { "cbrn", "biohazard", "hazmat" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <path d=""M 0 0 L -3.5 -6 A 7 7 0 0 1 3.5 -6 Z"" fill=""{COLOR}""/>
  <path d=""M 0 0 L 6.5 1 A 7 7 0 0 1 3 6.5 Z"" fill=""{COLOR}""/>
  <path d=""M 0 0 L -3 6.5 A 7 7 0 0 1 -6.5 1 Z"" fill=""{COLOR}""/>
  <circle cx=""0"" cy=""0"" r=""2.2"" fill=""#0f172a""/>
  <circle cx=""0"" cy=""0"" r=""1.2"" fill=""#ffffff""/>
</svg>");

            // =============================================================
            // CATEGORY 6: WAYPOINTS, NAVIGATION & MARITIME
            // =============================================================
            Register("waypoint", "Waypoint Flag", "Waypoints & Navigation", new[] { "flag", "rally" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <line x1=""-4"" y1=""-10"" x2=""-4"" y2=""10"" stroke=""#ffffff"" stroke-width=""2"" stroke-linecap=""round""/>
  <path d=""M -4 -10 L 7 -5.5 L -4 -1 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1"" stroke-linejoin=""round""/>
  <circle cx=""-4"" cy=""10"" r=""2"" fill=""#ffffff""/>
</svg>");

            Register("star", "HVT Star", "Waypoints & Navigation", new[] { "vip", "favorite" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <polygon points=""0,-8 2.3,-2.5 8,-2.5 3.5,1 5.3,6.5 0,3 -5.3,6.5 -3.5,1 -8,-2.5 -2.3,-2.5"" 
           fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""0.8"" stroke-linejoin=""round""/>
</svg>");

            Register("shield", "Shield Base", "Waypoints & Navigation", new[] { "friendly", "defense", "base" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M 0 -11 L 8.5 -8 L 8.5 1 C 8.5 6.5, 0 11.5, 0 11.5 C 0 11.5, -8.5 6.5, -8.5 1 L -8.5 -8 Z"" 
        fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2""/>
  <path d=""M 0 -9.5 L 7 -7 L 7 0.5 C 7 5, 0 9.5, 0 9.5 C 0 9.5, -7 5, -7 0.5 L -7 -7 Z"" 
        fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <path d=""M 0 -7 L 4.5 -5 L 4.5 0 C 4.5 3.5, 0 6.5, 0 6.5 C 0 6.5, -4.5 3.5, -4.5 0 L -4.5 -5 Z"" 
        fill=""{COLOR}""/>
</svg>");

            Register("pin", "Map Pin", "Waypoints & Navigation", new[] { "marker", "poi" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M 0 12 C 0 12, 8.5 2, 8.5 -3.5 C 8.5 -8.2, 4.7 -12, 0 -12 C -4.7 -12, -8.5 -8.2, -8.5 -3.5 C -8.5 2, 0 12, 0 12 Z"" 
        fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2"" stroke-linejoin=""round""/>
  <path d=""M 0 10.5 C 0 10.5, 7.5 1.5, 7.5 -3.5 C 7.5 -7.6, 4.1 -11, 0 -11 C -4.1 -11, -7.5 -7.6, -7.5 -3.5 C -7.5 1.5, 0 10.5, 0 10.5 Z"" 
        fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <circle cx=""0"" cy=""-3.5"" r=""3"" fill=""#0f172a""/>
  <circle cx=""-0.8"" cy=""-4.3"" r=""0.8"" fill=""#ffffff""/>
</svg>");

            Register("arrow", "Heading Arrow", "Waypoints & Navigation", new[] { "nav", "direction" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <path d=""M 0 -13 L 9.5 9 L 0 4 L -9.5 9 Z"" fill=""#000000"" fill-opacity=""0.5"" stroke=""#000000"" stroke-width=""2"" stroke-linejoin=""round""/>
  <path d=""M 0 -11 L 8 7 L 0 2.5 L -8 7 Z"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.4"" stroke-linejoin=""round""/>
  <circle cx=""0"" cy=""0"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("alert", "Alert Beacon", "Waypoints & Navigation", new[] { "hazard", "stop" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""-4,-10 4,-10 10,-4 10,4 4,10 -4,10 -10,4 -10,-4"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <polygon points=""1,-6 -3,0 0,0 -1,6 3,-1 0,-1"" fill=""#ffffff""/>
</svg>");

            Register("buoy", "Nav Buoy", "Waypoints & Navigation", new[] { "marine_buoy", "channel_marker" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <polygon points=""-3,-9 3,-9 6,2 -6,2"" fill=""{COLOR}"" stroke=""#ffffff"" stroke-width=""1.2""/>
  <polygon points=""-4,2 4,2 0,8"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1""/>
  <circle cx=""0"" cy=""-9"" r=""1.5"" fill=""#ffffff""/>
</svg>");

            Register("anchor", "Port Anchor", "Waypoints & Navigation", new[] { "harbor", "anchorage", "port" },
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-14 -14 28 28"" width=""28"" height=""28"">
  <circle cx=""0"" cy=""0"" r=""12"" fill=""#000000"" fill-opacity=""0.5""/>
  <circle cx=""0"" cy=""0"" r=""9.5"" fill=""#0f172a"" stroke=""#ffffff"" stroke-width=""1.4""/>
  <circle cx=""0"" cy=""-6"" r=""1.8"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.2""/>
  <line x1=""0"" y1=""-4"" x2=""0"" y2=""6"" stroke=""{COLOR}"" stroke-width=""1.5""/>
  <line x1=""-4"" y1=""-2"" x2=""4"" y2=""-2"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
  <path d=""M -6 2 C -6 7, 6 7, 6 2"" fill=""none"" stroke=""{COLOR}"" stroke-width=""1.5"" stroke-linecap=""round""/>
</svg>");
        }
    }
}
