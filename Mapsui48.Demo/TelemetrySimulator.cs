using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PelcoControlNM
{
    public enum SimulationRoute
    {
        DroneOrbit,
        SurveillanceSweep360,
        LinearPatrol,
        TargetLockOrbit,
        MountainFlight
    }

    public class TelemetryPacket
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AltitudeMeters { get; set; }
        public double GroundSpeedKmh { get; set; }
        public double Heading { get; set; }
        public double Pan { get; set; }
        public double Tilt { get; set; }
        public double Zoom { get; set; }
        public string RawNmea { get; set; }
        public string Description { get; set; }
    }

    public class TelemetrySimulator
    {
        private readonly PelcoControl _pelco;
        private System.Windows.Forms.Timer _timer;
        private double _elapsedSeconds = 0;
        private bool _isRunning = false;

        public SimulationRoute Route { get; set; } = SimulationRoute.DroneOrbit;
        public double CenterLat { get; set; } = 31.7767;
        public double CenterLon { get; set; } = 35.2345;
        public double TargetLat { get; set; } = 31.7767;
        public double TargetLon { get; set; } = 35.2345;
        public double OrbitRadiusKm { get; set; } = 1.2;
        public double FlightAltitudeMeters { get; set; } = 150.0;
        public double SpeedMultiplier { get; set; } = 1.0;

        public bool IsRunning => _isRunning;

        public event Action<TelemetryPacket> TelemetryUpdated;

        public TelemetrySimulator(PelcoControl pelco)
        {
            _pelco = pelco;
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100; // 10 Hz refresh rate
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _isRunning = true;
            _timer.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _timer.Stop();
        }

        public void Reset()
        {
            _elapsedSeconds = 0;
            UpdateStep(0);
        }

        public void SetInterval(int intervalMs)
        {
            _timer.Interval = Math.Max(20, intervalMs);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            double dt = (_timer.Interval / 1000.0) * SpeedMultiplier;
            _elapsedSeconds += dt;
            UpdateStep(_elapsedSeconds);
        }

        private void UpdateStep(double t)
        {
            double lat = CenterLat;
            double lon = CenterLon;
            double alt = FlightAltitudeMeters;
            double speedKmh = 60.0;
            double heading = 0.0;
            double pan = _pelco.CurrentPanNorthed;
            double tilt = _pelco.CurrentTilt;
            double zoom = _pelco.CurrentZoom;
            string desc = "";

            const double EarthRadiusKm = 6371.0;

            switch (Route)
            {
                case SimulationRoute.DroneOrbit:
                    {
                        // Circular UAV Orbit around CenterLat/CenterLon
                        double angularSpeed = 0.08; // rad/sec
                        double angle = t * angularSpeed;
                        double dLat = (OrbitRadiusKm / EarthRadiusKm) * (180.0 / Math.PI) * Math.Cos(angle);
                        double dLon = (OrbitRadiusKm / (EarthRadiusKm * Math.Cos(CenterLat * Math.PI / 180.0))) * (180.0 / Math.PI) * Math.Sin(angle);

                        lat = CenterLat + dLat;
                        lon = CenterLon + dLon;
                        alt = FlightAltitudeMeters + Math.Sin(t * 0.2) * 15.0; // Gentle altitude wave
                        speedKmh = 72.0;

                        // Vehicle heading is tangent to orbit circle
                        heading = (angle * 180.0 / Math.PI + 90.0 + 360.0) % 360.0;

                        // Camera searches ahead: forward-looking with ±35° scanning sweep
                        double sweep = Math.Sin(t * 1.5) * 35.0;
                        pan = (heading + sweep + 360.0) % 360.0;
                        tilt = -12.0 + Math.Sin(t * 0.8) * 4.0;
                        zoom = 28.0;
                        desc = $"UAV Orbit (R={OrbitRadiusKm:F1}km) | Scanning ±35°";
                        break;
                    }

                case SimulationRoute.TargetLockOrbit:
                    {
                        // Drone circles target while optical camera gimbal stays continuously locked on ground target
                        double angle = t * 0.06;
                        double dLat = (OrbitRadiusKm / EarthRadiusKm) * (180.0 / Math.PI) * Math.Cos(angle);
                        double dLon = (OrbitRadiusKm / (EarthRadiusKm * Math.Cos(TargetLat * Math.PI / 180.0))) * (180.0 / Math.PI) * Math.Sin(angle);

                        lat = TargetLat + dLat;
                        lon = TargetLon + dLon;
                        alt = FlightAltitudeMeters;
                        speedKmh = 65.0;
                        heading = (angle * 180.0 / Math.PI + 90.0 + 360.0) % 360.0;

                        // Calculate exact bearing from camera to ground target
                        double bearingToTarget = CalculateBearing(lat, lon, TargetLat, TargetLon);
                        double distMeters = DistanceMeters(lat, lon, TargetLat, TargetLon);
                        double pitchToTarget = Math.Atan2(-alt, distMeters) * 180.0 / Math.PI;

                        pan = bearingToTarget;
                        tilt = pitchToTarget;
                        zoom = 18.0;
                        desc = $"Target Lock-On (Range: {distMeters:F0}m, Tilt: {tilt:F1}°)";
                        break;
                    }

                case SimulationRoute.SurveillanceSweep360:
                    {
                        // Stationary high mast / tower, rotating 360 degrees smoothly
                        lat = CenterLat;
                        lon = CenterLon;
                        alt = FlightAltitudeMeters;
                        speedKmh = 0.0;
                        heading = 0.0;

                        // Continuous 360° rotation at 12° per second
                        pan = (t * 12.0) % 360.0;
                        tilt = -6.0 + Math.Sin(t * 0.3) * 3.0; // Slight tilt elevation oscillation
                        zoom = 30.0;
                        desc = $"Tower 360° Surveillance Sweep (Pan: {pan:F1}°)";
                        break;
                    }

                case SimulationRoute.LinearPatrol:
                    {
                        // Patrol vehicle driving back and forth along a 4km East-West corridor
                        double cycle = (t * 0.04) % (2.0 * Math.PI);
                        double offsetKm = Math.Sin(cycle) * 2.0;
                        double dLon = (offsetKm / (EarthRadiusKm * Math.Cos(CenterLat * Math.PI / 180.0))) * (180.0 / Math.PI);

                        lat = CenterLat;
                        lon = CenterLon + dLon;
                        alt = 8.0; // Vehicle roof height
                        speedKmh = 50.0;
                        heading = Math.Cos(cycle) >= 0 ? 90.0 : 270.0;

                        // Camera sweeps along road side
                        pan = (heading + Math.Sin(t * 1.8) * 45.0 + 360.0) % 360.0;
                        tilt = -4.0;
                        zoom = 35.0;
                        desc = $"Patrol Vehicle (Heading: {heading:F0}°, Offset: {offsetKm:F2}km)";
                        break;
                    }

                case SimulationRoute.MountainFlight:
                    {
                        // Aircraft traversing mountainous terrain
                        double progress = (t * 0.03) % (2.0 * Math.PI);
                        double dLat = (Math.Sin(progress) * 3.0 / EarthRadiusKm) * (180.0 / Math.PI);
                        double dLon = (Math.Cos(progress * 2.0) * 2.0 / (EarthRadiusKm * Math.Cos(CenterLat * Math.PI / 180.0))) * (180.0 / Math.PI);

                        lat = CenterLat + dLat;
                        lon = CenterLon + dLon;
                        alt = FlightAltitudeMeters + Math.Sin(t * 0.5) * 80.0; // Altitude changes over terrain
                        speedKmh = 140.0;
                        heading = (Math.Atan2(Math.Cos(progress * 2.0) * -4.0, Math.Cos(progress) * 3.0) * 180.0 / Math.PI + 360.0) % 360.0;

                        pan = (heading + Math.Sin(t) * 20.0 + 360.0) % 360.0;
                        tilt = -18.0 + Math.Cos(t * 0.4) * 6.0;
                        zoom = 22.0;
                        desc = $"Mountain Ridge Recon (Alt: {alt:F0}m, Speed: {speedKmh:F0}km/h)";
                        break;
                    }
            }

            // Update the Pelco state model
            _pelco.CurrentPanNorthed = pan;
            _pelco.CurrentTilt = tilt;
            _pelco.CurrentZoom = zoom;

            // Synthesize standard NMEA GPS string ($GPGGA)
            string nmea = GenerateNmeaGga(lat, lon, alt);

            var packet = new TelemetryPacket
            {
                Timestamp = DateTime.UtcNow,
                Latitude = lat,
                Longitude = lon,
                AltitudeMeters = alt,
                GroundSpeedKmh = speedKmh,
                Heading = heading,
                Pan = pan,
                Tilt = tilt,
                Zoom = zoom,
                RawNmea = nmea,
                Description = desc
            };

            TelemetryUpdated?.Invoke(packet);
        }

        private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double phi1 = lat1 * Math.PI / 180.0;
            double phi2 = lat2 * Math.PI / 180.0;
            double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;

            double y = Math.Sin(deltaLambda) * Math.Cos(phi2);
            double x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);

            double bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (bearing + 360.0) % 360.0;
        }

        private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static string GenerateNmeaGga(double lat, double lon, double alt)
        {
            int latDeg = (int)Math.Abs(lat);
            double latMin = (Math.Abs(lat) - latDeg) * 60.0;
            char latDir = lat >= 0 ? 'N' : 'S';

            int lonDeg = (int)Math.Abs(lon);
            double lonMin = (Math.Abs(lon) - lonDeg) * 60.0;
            char lonDir = lon >= 0 ? 'E' : 'W';

            string timeStr = DateTime.UtcNow.ToString("HHmmss.ff");
            string payload = $"GPGGA,{timeStr},{latDeg:D2}{latMin:00.0000},{latDir},{lonDeg:D3}{lonMin:00.0000},{lonDir},1,08,0.9,{alt:F1},M,0.0,M,,";

            // Compute NMEA XOR checksum
            byte checksum = 0;
            for (int i = 0; i < payload.Length; i++)
            {
                checksum ^= (byte)payload[i];
            }

            return $"${payload}*{checksum:X2}";
        }
    }
}
