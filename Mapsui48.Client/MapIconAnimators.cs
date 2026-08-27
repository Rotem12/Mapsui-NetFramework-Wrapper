using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Mapsui48.Client
{
    /// <summary>
    /// High-performance GDI+ vector animation renderers for tactical context action icons.
    /// Provides continuous 60 FPS looping animations when hovered.
    /// </summary>
    public static class MapIconAnimators
    {
        public static void DrawAnimatedIcon(
            Graphics g,
            RectangleF bounds,
            MapContextMenuItem item,
            float phase,
            bool isHovered)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color accent = item.AccentColor ?? Color.FromArgb(0, 229, 255); // Cyan default

            // 1. Custom user delegate if provided
            if (item.CustomDrawIcon != null)
            {
                item.CustomDrawIcon(g, bounds, phase, isHovered);
                return;
            }

            // 2. Specific procedural animation types
            switch (item.AnimationType)
            {
                case IconAnimationType.Elevation3D:
                    DrawGotoElevation(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.CompassBearing:
                    DrawGotoBearing(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.RadarSweep:
                    DrawRadarSweep(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.SonarWave:
                    DrawSonarWave(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.CrosshairLock:
                    DrawCrosshairLock(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.ThermalScan:
                    DrawThermalScan(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.MissileTrajectory:
                    DrawMissileTrajectory(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.OrbitSatellite:
                    DrawOrbitSatellite(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.ShieldDefense:
                    DrawShieldDefense(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.LaserRangefinder:
                    DrawLaserRangefinder(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.StrobeAlert:
                    DrawStrobeAlert(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.FlightHorizon:
                    DrawFlightHorizon(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.MaritimeWake:
                    DrawMaritimeWake(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.FlagWaving:
                    DrawFlagWaving(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.SniperScope:
                    DrawSniperScope(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.FlamePulse:
                    DrawFlamePulse(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.HelipadLZ:
                    DrawHelipadLZ(g, bounds, accent, phase, isHovered);
                    return;

                case IconAnimationType.Pulse:
                    DrawPulsingIcon(g, bounds, item.IconKey, accent, phase, isHovered);
                    return;

                case IconAnimationType.Rotate:
                    DrawRotatingIcon(g, bounds, item.IconKey, accent, phase, isHovered);
                    return;

                case IconAnimationType.Bounce:
                    DrawBouncingIcon(g, bounds, item.IconKey, accent, phase, isHovered);
                    return;

                case IconAnimationType.None:
                default:
                    DrawStaticIcon(g, bounds, item.IconKey, accent, isHovered ? 1.15f : 1.0f);
                    return;
            }
        }

        // =========================================================================
        // GOTO WITH ELEVATION (3D Isometric Pitch & Altitude Trajectory Animation)
        // =========================================================================
        public static void DrawGotoElevation(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float size = Math.Min(bounds.Width, bounds.Height);

            float gx0 = cx - size * 0.32f;
            float gy0 = cy + size * 0.28f;
            float gx1 = cx + size * 0.30f;
            float gy1 = cy + size * 0.18f;
            float tx = gx1;
            float ty = cy - size * 0.28f;

            // 1. 3D Isometric Ground Grid Plane
            using (var gridPen = new Pen(Color.FromArgb(isHovered ? 90 : 50, accent), 1f))
            {
                gridPen.DashStyle = DashStyle.Dot;
                PointF[] groundDiamond = {
                    new PointF(cx - size * 0.36f, cy + size * 0.22f),
                    new PointF(cx, cy + size * 0.40f),
                    new PointF(cx + size * 0.36f, cy + size * 0.16f),
                    new PointF(cx, cy - size * 0.02f)
                };
                g.DrawPolygon(gridPen, groundDiamond);
            }

            // 2. Vertical Altitude Pole
            using (var polePen = new Pen(Color.FromArgb(isHovered ? 180 : 100, 255, 255, 255), 1.2f))
            {
                polePen.DashStyle = DashStyle.Dash;
                g.DrawLine(polePen, gx1, gy1, tx, ty);
            }

            // Altitude Ticks
            using (var tickPen = new Pen(Color.FromArgb(isHovered ? 220 : 120, accent), 1f))
            {
                for (int i = 1; i <= 3; i++)
                {
                    float tickY = gy1 + (ty - gy1) * (i / 4f);
                    g.DrawLine(tickPen, gx1 - 2.5f, tickY, gx1 + 2.5f, tickY);
                }
            }

            // 3. Animated Elevation Wave Traveling Vertically
            if (isHovered)
            {
                float waveFrac = (float)((Math.Sin(phase * 3.5f) + 1.0) / 2.0);
                float waveY = gy1 + (ty - gy1) * waveFrac;

                using (var waveBrush = new SolidBrush(Color.FromArgb(200, accent)))
                using (var wavePen = new Pen(Color.White, 1.4f))
                {
                    g.FillEllipse(waveBrush, gx1 - 5f, waveY - 2f, 10f, 4f);
                    g.DrawEllipse(wavePen, gx1 - 5f, waveY - 2f, 10f, 4f);
                }

                float trajFrac = (phase * 1.2f) % 1.0f;
                float px = gx0 + (tx - gx0) * trajFrac;
                float py = (float)(gy0 + (ty - gy0) * trajFrac - Math.Sin(trajFrac * Math.PI) * (size * 0.15f));
                using (var particleBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(particleBrush, px - 2f, py - 2f, 4f, 4f);
                }
            }

            // 4. Ballistic 3D Trajectory Arc
            using (var trajPen = new Pen(accent, isHovered ? 2.2f : 1.8f))
            {
                trajPen.LineJoin = LineJoin.Round;
                using (var path = new GraphicsPath())
                {
                    float ctrlX = (gx0 + tx) / 2f - size * 0.05f;
                    float ctrlY = Math.Min(gy0, ty) - size * 0.18f;
                    path.AddBezier(gx0, gy0, ctrlX, ctrlY, (gx0 + tx) / 2f, ty, tx, ty);
                    g.DrawPath(trajPen, path);
                }
            }

            // 5. Origin Base Marker
            using (var baseBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            {
                g.FillEllipse(baseBrush, gx0 - 2.5f, gy0 - 2.5f, 5f, 5f);
            }

            // 6. Elevated Target Crosshair / Reticle
            float targetPulse = isHovered ? (float)(1.0 + 0.25 * Math.Sin(phase * 5.0f)) : 1.0f;
            float tr = 6.5f * targetPulse;

            if (isHovered)
            {
                using (var auraBrush = new SolidBrush(Color.FromArgb(70, accent)))
                {
                    g.FillEllipse(auraBrush, tx - tr * 1.5f, ty - tr * 1.5f, tr * 3f, tr * 3f);
                }
            }

            using (var targetPen = new Pen(accent, 1.8f))
            using (var whitePen = new Pen(Color.White, 1.2f))
            {
                g.DrawEllipse(targetPen, tx - tr, ty - tr, tr * 2f, tr * 2f);
                g.DrawLine(whitePen, tx - tr - 3f, ty, tx + tr + 3f, ty);
                g.DrawLine(whitePen, tx, ty - tr - 3f, tx, ty + tr + 3f);
            }
            using (var dotBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(dotBrush, tx - 1.5f, ty - 1.5f, 3f, 3f);
            }

            // 7. Small Elevation Indicator Tag
            using (var arrowPen = new Pen(Color.FromArgb(isHovered ? 255 : 180, accent), 1.4f))
            {
                float ax = gx1 + 5f;
                g.DrawLine(arrowPen, ax, gy1 - 2, ax, ty + 2);
                g.DrawLine(arrowPen, ax - 2f, ty + 5, ax, ty + 2);
                g.DrawLine(arrowPen, ax + 2f, ty + 5, ax, ty + 2);
            }
        }

        // =========================================================================
        // GOTO WITHOUT ELEVATION (2D Tactical Azimuth Compass & Radar Sweep)
        // =========================================================================
        public static void DrawGotoBearing(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            // 1. Outer Compass Ring
            using (var ringPen = new Pen(Color.FromArgb(isHovered ? 180 : 100, accent), 1.5f))
            {
                g.DrawEllipse(ringPen, cx - r, cy - r, r * 2f, r * 2f);
            }

            // 2. Degree Tick Marks
            float ringRotation = isHovered ? (phase * 15f) : 0f;
            using (var tickPen = new Pen(Color.FromArgb(isHovered ? 160 : 90, 255, 255, 255), 1f))
            {
                for (int deg = 0; deg < 360; deg += 30)
                {
                    double rad = (deg + ringRotation) * Math.PI / 180.0;
                    float len = (deg % 90 == 0) ? 4.5f : 2.5f;
                    float x1 = cx + (float)(Math.Cos(rad) * r);
                    float y1 = cy + (float)(Math.Sin(rad) * r);
                    float x2 = cx + (float)(Math.Cos(rad) * (r - len));
                    float y2 = cy + (float)(Math.Sin(rad) * (r - len));
                    g.DrawLine(tickPen, x1, y1, x2, y2);
                }
            }

            // 3. Rotating Azimuth Radar Scan Sweep Cone
            if (isHovered)
            {
                float sweepAngle = (phase * 180f) % 360f;
                float sweepSpan = 45f;

                using (var sweepPath = new GraphicsPath())
                {
                    sweepPath.AddPie(cx - r + 1, cy - r + 1, (r - 1) * 2f, (r - 1) * 2f, sweepAngle - sweepSpan, sweepSpan);
                    using (var sweepBrush = new PathGradientBrush(sweepPath))
                    {
                        sweepBrush.CenterPoint = new PointF(cx, cy);
                        sweepBrush.CenterColor = Color.FromArgb(140, accent);
                        sweepBrush.SurroundColors = new[] { Color.FromArgb(0, accent) };
                        g.FillPath(sweepBrush, sweepPath);
                    }
                }

                double beamRad = sweepAngle * Math.PI / 180.0;
                float bx = cx + (float)(Math.Cos(beamRad) * r);
                float by = cy + (float)(Math.Sin(beamRad) * r);
                using (var beamPen = new Pen(Color.White, 1.6f))
                {
                    g.DrawLine(beamPen, cx, cy, bx, by);
                }
            }

            // 4. Azimuth Heading Arrow
            float pointerAngle = isHovered ? (float)(45.0 + Math.Sin(phase * 4.0f) * 6.0) : 45f;
            double ptrRad = (pointerAngle - 90.0) * Math.PI / 180.0;

            float tipX = cx + (float)(Math.Cos(ptrRad) * (r * 0.85f));
            float tipY = cy + (float)(Math.Sin(ptrRad) * (r * 0.85f));

            double leftRad = (pointerAngle - 90.0 + 150.0) * Math.PI / 180.0;
            double rightRad = (pointerAngle - 90.0 - 150.0) * Math.PI / 180.0;

            float lx = cx + (float)(Math.Cos(leftRad) * (r * 0.40f));
            float ly = cy + (float)(Math.Sin(leftRad) * (r * 0.40f));
            float rx = cx + (float)(Math.Cos(rightRad) * (r * 0.40f));
            float ry = cy + (float)(Math.Sin(rightRad) * (r * 0.40f));

            PointF[] northHalf = { new PointF(cx, cy), new PointF(tipX, tipY), new PointF(lx, ly) };
            PointF[] southHalf = { new PointF(cx, cy), new PointF(tipX, tipY), new PointF(rx, ry) };

            using (var nBrush = new SolidBrush(Color.FromArgb(isHovered ? 255 : 220, accent)))
            using (var sBrush = new SolidBrush(Color.FromArgb(isHovered ? 200 : 150, Color.White)))
            using (var needlePen = new Pen(Color.FromArgb(20, 20, 20), 1f))
            {
                g.FillPolygon(nBrush, northHalf);
                g.FillPolygon(sBrush, southHalf);
                g.DrawPolygon(needlePen, new[] { new PointF(tipX, tipY), new PointF(lx, ly), new PointF(cx, cy), new PointF(rx, ry) });
            }

            // 5. Central Hub Pivot
            using (var hubBrush = new SolidBrush(Color.FromArgb(20, 28, 38)))
            using (var hubRingPen = new Pen(Color.White, 1.2f))
            using (var dotBrush = new SolidBrush(accent))
            {
                g.FillEllipse(hubBrush, cx - 3.5f, cy - 3.5f, 7f, 7f);
                g.DrawEllipse(hubRingPen, cx - 3.5f, cy - 3.5f, 7f, 7f);
                g.FillEllipse(dotBrush, cx - 1.5f, cy - 1.5f, 3f, 3f);
            }

            // 6. Cardinal "N" Indicator at top
            using (var nFont = new Font("Arial", 6.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(isHovered ? 255 : 180, 239, 68, 68)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("N", nFont, textBrush, cx, cy - r - 5f, sf);
            }
        }

        // =========================================================================
        // RADAR SWEEP ANIMATION
        // =========================================================================
        public static void DrawRadarSweep(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            using (var ringPen = new Pen(Color.FromArgb(isHovered ? 120 : 60, accent), 1f))
            {
                g.DrawEllipse(ringPen, cx - r, cy - r, r * 2f, r * 2f);
                g.DrawEllipse(ringPen, cx - r * 0.65f, cy - r * 0.65f, r * 1.3f, r * 1.3f);
                g.DrawEllipse(ringPen, cx - r * 0.30f, cy - r * 0.30f, r * 0.6f, r * 0.6f);
                g.DrawLine(ringPen, cx - r, cy, cx + r, cy);
                g.DrawLine(ringPen, cx, cy - r, cx, cy + r);
            }

            float sweepAngle = (phase * 200f) % 360f;
            float span = 50f;
            using (var path = new GraphicsPath())
            {
                path.AddPie(cx - r, cy - r, r * 2f, r * 2f, sweepAngle - span, span);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = Color.FromArgb(isHovered ? 160 : 90, accent);
                    brush.SurroundColors = new[] { Color.FromArgb(0, accent) };
                    g.FillPath(brush, path);
                }
            }

            double rad = sweepAngle * Math.PI / 180.0;
            using (var beamPen = new Pen(Color.White, 1.5f))
            {
                g.DrawLine(beamPen, cx, cy, cx + (float)(Math.Cos(rad) * r), cy + (float)(Math.Sin(rad) * r));
            }

            float blipAngle = 135f;
            float blipDist = r * 0.6f;
            double blipRad = blipAngle * Math.PI / 180.0;
            float bx = cx + (float)(Math.Cos(blipRad) * blipDist);
            float by = cy + (float)(Math.Sin(blipRad) * blipDist);

            float angleDiff = Math.Abs((sweepAngle - blipAngle + 360) % 360);
            int blipAlpha = angleDiff < 60 ? (int)(255 * (1.0 - angleDiff / 60.0)) : 40;
            using (var blipBrush = new SolidBrush(Color.FromArgb(blipAlpha, 239, 68, 68)))
            {
                g.FillEllipse(blipBrush, bx - 2.5f, by - 2.5f, 5f, 5f);
            }
        }

        // =========================================================================
        // SONAR WAVE ANIMATION
        // =========================================================================
        public static void DrawSonarWave(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float maxR = Math.Min(bounds.Width, bounds.Height) * 0.44f;

            for (int i = 0; i < 3; i++)
            {
                float ringPhase = (phase * 1.5f + i * 0.33f) % 1.0f;
                float r = maxR * ringPhase;
                int alpha = (int)(220 * (1.0f - ringPhase));
                if (alpha > 0)
                {
                    using (var pen = new Pen(Color.FromArgb(alpha, accent), 1.5f))
                    {
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
                    }
                }
            }

            using (var dotBrush = new SolidBrush(Color.White))
            using (var auraBrush = new SolidBrush(Color.FromArgb(160, accent)))
            {
                g.FillEllipse(auraBrush, cx - 5f, cy - 5f, 10f, 10f);
                g.FillEllipse(dotBrush, cx - 2.5f, cy - 2.5f, 5f, 5f);
            }
        }

        // =========================================================================
        // CROSSHAIR LOCK ANIMATION
        // =========================================================================
        public static void DrawCrosshairLock(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float size = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            float bracketOffset = isHovered ? (float)(size * (0.8 + 0.2 * Math.Sin(phase * 6.0f))) : size;
            float bLen = size * 0.4f;

            using (var bracketPen = new Pen(accent, 2f))
            {
                // Top-Left
                g.DrawLine(bracketPen, cx - bracketOffset, cy - bracketOffset + bLen, cx - bracketOffset, cy - bracketOffset);
                g.DrawLine(bracketPen, cx - bracketOffset, cy - bracketOffset, cx - bracketOffset + bLen, cy - bracketOffset);

                // Top-Right
                g.DrawLine(bracketPen, cx + bracketOffset - bLen, cy - bracketOffset, cx + bracketOffset, cy - bracketOffset);
                g.DrawLine(bracketPen, cx + bracketOffset, cy - bracketOffset, cx + bracketOffset, cy - bracketOffset + bLen);

                // Bottom-Left
                g.DrawLine(bracketPen, cx - bracketOffset, cy + bracketOffset - bLen, cx - bracketOffset, cy + bracketOffset);
                g.DrawLine(bracketPen, cx - bracketOffset, cy + bracketOffset, cx - bracketOffset + bLen, cy + bracketOffset);

                // Bottom-Right
                g.DrawLine(bracketPen, cx + bracketOffset - bLen, cy + bracketOffset, cx + bracketOffset, cy + bracketOffset);
                g.DrawLine(bracketPen, cx + bracketOffset, cy + bracketOffset, cx + bracketOffset, cy + bracketOffset - bLen);
            }

            using (var crossPen = new Pen(Color.White, 1.2f))
            using (var dotBrush = new SolidBrush(accent))
            {
                g.DrawLine(crossPen, cx - 4, cy, cx + 4, cy);
                g.DrawLine(crossPen, cx, cy - 4, cx, cy + 4);
                g.FillEllipse(dotBrush, cx - 1.5f, cy - 1.5f, 3f, 3f);
            }
        }

        // =========================================================================
        // THERMAL SCAN ANIMATION (FLIR IR Raster Scanline)
        // =========================================================================
        public static void DrawThermalScan(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            // Sensor Box
            var rect = new RectangleF(cx - r, cy - r * 0.8f, r * 2f, r * 1.6f);
            using (var boxBrush = new SolidBrush(Color.FromArgb(160, 15, 23, 42)))
            using (var boxPen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.2f))
            {
                g.FillRectangle(boxBrush, rect);
                g.DrawRectangle(boxPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // Dual IR Lens Circles
            float lx1 = cx - r * 0.42f;
            float lx2 = cx + r * 0.42f;
            float ly = cy;
            float lr = r * 0.36f;

            using (var lensBrush = new SolidBrush(Color.FromArgb(200, 30, 41, 59)))
            using (var lensPen = new Pen(Color.FromArgb(220, 245, 158, 11), 1.2f)) // Amber IR
            {
                g.FillEllipse(lensBrush, lx1 - lr, ly - lr, lr * 2f, lr * 2f);
                g.DrawEllipse(lensPen, lx1 - lr, ly - lr, lr * 2f, lr * 2f);

                g.FillEllipse(lensBrush, lx2 - lr, ly - lr, lr * 2f, lr * 2f);
                g.DrawEllipse(lensPen, lx2 - lr, ly - lr, lr * 2f, lr * 2f);
            }

            // Thermal Raster Scanline
            if (isHovered)
            {
                float scanY = rect.Y + (float)((Math.Sin(phase * 4.0f) + 1.0) / 2.0 * rect.Height);
                using (var scanPen = new Pen(Color.FromArgb(255, 239, 68, 68), 1.6f)) // Hot red scanline
                using (var glowPen = new Pen(Color.FromArgb(90, 245, 158, 11), 4f))
                {
                    g.DrawLine(glowPen, rect.Left + 2, scanY, rect.Right - 2, scanY);
                    g.DrawLine(scanPen, rect.Left + 2, scanY, rect.Right - 2, scanY);
                }
            }

            // IR Tag
            using (var tagFont = new Font("Arial", 5.5f, FontStyle.Bold))
            using (var tagBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
            {
                g.DrawString("IR", tagFont, tagBrush, cx - 4, cy - r * 0.7f);
            }
        }

        // =========================================================================
        // MISSILE / BALLISTIC TRAJECTORY ANIMATION
        // =========================================================================
        public static void DrawMissileTrajectory(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float size = Math.Min(bounds.Width, bounds.Height);

            float x0 = cx - size * 0.35f;
            float y0 = cy + size * 0.28f;
            float x1 = cx + size * 0.32f;
            float y1 = cy + size * 0.25f;
            float apexX = cx - size * 0.05f;
            float apexY = cy - size * 0.32f;

            // Trajectory Arc
            using (var path = new GraphicsPath())
            {
                path.AddBezier(x0, y0, apexX - size * 0.15f, apexY, apexX + size * 0.15f, apexY, x1, y1);
                using (var arcPen = new Pen(Color.FromArgb(isHovered ? 180 : 100, 239, 68, 68), 1.5f))
                {
                    arcPen.DashStyle = DashStyle.Dash;
                    g.DrawPath(arcPen, path);
                }
            }

            // Animated Missile on Trajectory
            float t = isHovered ? ((phase * 1.3f) % 1.0f) : 0.6f;
            // Quadratic Bezier approximation
            float u = 1 - t;
            float mx = u * u * x0 + 2 * u * t * apexX + t * t * x1;
            float my = u * u * y0 + 2 * u * t * apexY + t * t * y1;

            // Missile body & rocket flame
            using (var flameBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (var missileBrush = new SolidBrush(Color.White))
            using (var pen = new Pen(Color.FromArgb(239, 68, 68), 1f))
            {
                if (isHovered)
                {
                    g.FillEllipse(flameBrush, mx - 3f, my + 1f, 6f, 6f);
                }
                g.FillEllipse(missileBrush, mx - 2.5f, my - 2.5f, 5f, 5f);
                g.DrawEllipse(pen, mx - 2.5f, my - 2.5f, 5f, 5f);
            }

            // Ground target blast ring
            using (var blastPen = new Pen(Color.FromArgb(239, 68, 68), 1.2f))
            {
                g.DrawEllipse(blastPen, x1 - 5f, y1 - 2.5f, 10f, 5f);
            }
        }

        // =========================================================================
        // ORBIT SATELLITE ANIMATION
        // =========================================================================
        public static void DrawOrbitSatellite(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float rx = Math.Min(bounds.Width, bounds.Height) * 0.42f;
            float ry = rx * 0.45f;

            // Central Planet / Earth Station
            using (var planetBrush = new SolidBrush(Color.FromArgb(30, 58, 138)))
            using (var planetPen = new Pen(Color.FromArgb(0, 229, 255), 1.2f))
            {
                g.FillEllipse(planetBrush, cx - 6f, cy - 6f, 12f, 12f);
                g.DrawEllipse(planetPen, cx - 6f, cy - 6f, 12f, 12f);
            }

            // Orbit Ring (Tilted)
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-25f);
            using (var orbitPen = new Pen(Color.FromArgb(isHovered ? 140 : 70, accent), 1f))
            {
                orbitPen.DashStyle = DashStyle.Dot;
                g.DrawEllipse(orbitPen, -rx, -ry, rx * 2f, ry * 2f);
            }

            // Satellite Position along Orbit
            float orbitAngle = (phase * 160f) % 360f;
            double rad = orbitAngle * Math.PI / 180.0;
            float satX = (float)(Math.Cos(rad) * rx);
            float satY = (float)(Math.Sin(rad) * ry);

            // Satellite Solar Panels & Body
            using (var bodyBrush = new SolidBrush(Color.White))
            using (var panelBrush = new SolidBrush(Color.FromArgb(0, 229, 255)))
            {
                g.FillRectangle(panelBrush, satX - 6f, satY - 1.5f, 12f, 3f);
                g.FillEllipse(bodyBrush, satX - 2.5f, satY - 2.5f, 5f, 5f);
            }

            // Downlink Beam from Sat to Center
            if (isHovered && Math.Sin(rad) < 0)
            {
                using (var beamPen = new Pen(Color.FromArgb(120, 0, 229, 255), 1f))
                {
                    beamPen.DashStyle = DashStyle.Dash;
                    g.DrawLine(beamPen, satX, satY, 0, 0);
                }
            }

            g.Restore(state);
        }

        // =========================================================================
        // SHIELD DEFENSE ANIMATION (Hexagonal Barrier Ripple)
        // =========================================================================
        public static void DrawShieldDefense(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            // Hexagon Points
            PointF[] hex = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                double a = (i * 60 - 30) * Math.PI / 180.0;
                hex[i] = new PointF(cx + (float)(Math.Cos(a) * r), cy + (float)(Math.Sin(a) * r));
            }

            using (var shieldBrush = new SolidBrush(Color.FromArgb(isHovered ? 100 : 50, Color.FromArgb(34, 197, 94)))) // Green shield
            using (var shieldPen = new Pen(Color.FromArgb(34, 197, 94), 1.8f))
            {
                g.FillPolygon(shieldBrush, hex);
                g.DrawPolygon(shieldPen, hex);
            }

            // Energy Ripple Wave
            if (isHovered)
            {
                float ripFrac = (phase * 2.0f) % 1.0f;
                float ripR = r * ripFrac;
                PointF[] innerHex = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    double a = (i * 60 - 30) * Math.PI / 180.0;
                    innerHex[i] = new PointF(cx + (float)(Math.Cos(a) * ripR), cy + (float)(Math.Sin(a) * ripR));
                }
                int ripAlpha = (int)(220 * (1.0f - ripFrac));
                using (var ripPen = new Pen(Color.FromArgb(ripAlpha, Color.White), 1.4f))
                {
                    g.DrawPolygon(ripPen, innerHex);
                }
            }

            // Central Shield Crest / Cross
            using (var crestPen = new Pen(Color.White, 2f))
            {
                g.DrawLine(crestPen, cx, cy - r * 0.45f, cx, cy + r * 0.45f);
                g.DrawLine(crestPen, cx - r * 0.45f, cy, cx + r * 0.45f, cy);
            }
        }

        // =========================================================================
        // LASER RANGEFINDER ANIMATION
        // =========================================================================
        public static void DrawLaserRangefinder(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float size = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            float x0 = cx - size * 0.8f;
            float y0 = cy;
            float x1 = cx + size * 0.8f;
            float y1 = cy;

            // Laser Device Icon (Left)
            using (var devBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var devPen = new Pen(Color.White, 1.2f))
            {
                g.FillRectangle(devBrush, x0 - 4, y0 - 6, 8, 12);
                g.DrawRectangle(devPen, x0 - 4, y0 - 6, 8, 12);
            }

            // Pulsing Laser Beam
            Color laserColor = Color.FromArgb(239, 68, 68); // Ruby Red
            using (var beamPen = new Pen(laserColor, isHovered ? 2.2f : 1.4f))
            using (var glowPen = new Pen(Color.FromArgb(isHovered ? 120 : 50, laserColor), 5f))
            {
                g.DrawLine(glowPen, x0 + 4, y0, x1 - 3, y1);
                g.DrawLine(beamPen, x0 + 4, y0, x1 - 3, y1);
            }

            // Distance Pulse Particles
            if (isHovered)
            {
                float dFrac = (phase * 3.0f) % 1.0f;
                float px = (x0 + 4) + (x1 - x0 - 7) * dFrac;
                using (var pBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(pBrush, px - 2, y0 - 2, 4, 4);
                }
            }

            // Target Reflector Dot
            using (var targetBrush = new SolidBrush(Color.White))
            using (var auraBrush = new SolidBrush(Color.FromArgb(200, laserColor)))
            {
                g.FillEllipse(auraBrush, x1 - 4, y1 - 4, 8, 8);
                g.FillEllipse(targetBrush, x1 - 2, y1 - 2, 4, 4);
            }
        }

        // =========================================================================
        // STROBE ALERT ANIMATION
        // =========================================================================
        public static void DrawStrobeAlert(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            bool flashState = !isHovered || (Math.Sin(phase * 18.0f) > 0);
            Color alertCol = flashState ? Color.FromArgb(239, 68, 68) : Color.FromArgb(245, 158, 11);

            PointF[] tri = {
                new PointF(cx, cy - r),
                new PointF(cx + r * 0.95f, cy + r * 0.85f),
                new PointF(cx - r * 0.95f, cy + r * 0.85f)
            };

            using (var triBrush = new SolidBrush(Color.FromArgb(isHovered ? 220 : 180, alertCol)))
            using (var pen = new Pen(Color.White, 1.6f))
            {
                g.FillPolygon(triBrush, tri);
                g.DrawPolygon(pen, tri);
            }

            // Exclamation Symbol
            using (var wPen = new Pen(Color.White, 2.2f))
            using (var dotBrush = new SolidBrush(Color.White))
            {
                g.DrawLine(wPen, cx, cy - r * 0.35f, cx, cy + r * 0.20f);
                g.FillEllipse(dotBrush, cx - 1.5f, cy + r * 0.45f, 3f, 3f);
            }
        }

        // =========================================================================
        // FLIGHT HORIZON ANIMATION (Artificial Horizon Gyro)
        // =========================================================================
        public static void DrawFlightHorizon(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            // Outer Instrument Bezel
            using (var bezelPen = new Pen(Color.FromArgb(isHovered ? 200 : 100, accent), 1.5f))
            {
                g.DrawEllipse(bezelPen, cx - r, cy - r, r * 2f, r * 2f);
            }

            // Banking Roll & Pitch
            float roll = isHovered ? (float)(Math.Sin(phase * 3.0f) * 20.0) : 0f;
            float pitch = isHovered ? (float)(Math.Cos(phase * 3.0f) * 4.0) : 0f;

            var state = g.Save();
            g.TranslateTransform(cx, cy + pitch);
            g.RotateTransform(roll);

            // Sky / Ground Division Line
            using (var horizonPen = new Pen(Color.White, 2f))
            {
                g.DrawLine(horizonPen, -r * 0.75f, 0, r * 0.75f, 0);
            }
            // Pitch Ladder Ticks
            using (var tickPen = new Pen(Color.FromArgb(0, 229, 255), 1.2f))
            {
                g.DrawLine(tickPen, -r * 0.4f, -6, r * 0.4f, -6);
                g.DrawLine(tickPen, -r * 0.4f, 6, r * 0.4f, 6);
            }

            g.Restore(state);

            // Fixed Aircraft Datum Symbol (W)
            using (var craftPen = new Pen(Color.FromArgb(245, 158, 11), 2.2f)) // Amber
            {
                g.DrawLine(craftPen, cx - r * 0.45f, cy, cx - r * 0.15f, cy);
                g.DrawLine(craftPen, cx - r * 0.15f, cy, cx, cy + 3);
                g.DrawLine(craftPen, cx, cy + 3, cx + r * 0.15f, cy);
                g.DrawLine(craftPen, cx + r * 0.15f, cy, cx + r * 0.45f, cy);
            }
        }

        // =========================================================================
        // MARITIME WAKE ANIMATION
        // =========================================================================
        public static void DrawMaritimeWake(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            // Vessel Hull
            PointF[] hull = {
                new PointF(cx, cy - r * 0.8f),
                new PointF(cx + r * 0.4f, cy + r * 0.1f),
                new PointF(cx + r * 0.35f, cy + r * 0.7f),
                new PointF(cx - r * 0.35f, cy + r * 0.7f),
                new PointF(cx - r * 0.4f, cy + r * 0.1f)
            };

            using (var hullBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var hullPen = new Pen(Color.White, 1.4f))
            {
                g.FillPolygon(hullBrush, hull);
                g.DrawPolygon(hullPen, hull);
            }

            // Animated V-shaped Bow Wake
            if (isHovered)
            {
                for (int i = 0; i < 2; i++)
                {
                    float wPhase = (phase * 2.0f + i * 0.5f) % 1.0f;
                    float wY = cy - r * 0.7f + wPhase * (r * 1.3f);
                    float wSpread = wPhase * (r * 0.9f);
                    int alpha = (int)(200 * (1.0f - wPhase));

                    using (var wakePen = new Pen(Color.FromArgb(alpha, 0, 229, 255), 1.2f))
                    {
                        g.DrawLine(wakePen, cx, wY, cx - wSpread, wY + 6);
                        g.DrawLine(wakePen, cx, wY, cx + wSpread, wY + 6);
                    }
                }
            }
        }

        // =========================================================================
        // FLAG WAVING ANIMATION
        // =========================================================================
        public static void DrawFlagWaving(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            float poleX = cx - r * 0.5f;
            float poleTopY = cy - r * 0.85f;
            float poleBotY = cy + r * 0.85f;

            // Flagpole
            using (var polePen = new Pen(Color.White, 2f))
            using (var finialBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
            {
                g.DrawLine(polePen, poleX, poleTopY, poleX, poleBotY);
                g.FillEllipse(finialBrush, poleX - 2.5f, poleTopY - 2.5f, 5f, 5f);
            }

            // Waving Flag Cloth
            float flagW = r * 1.1f;
            float flagH = r * 0.75f;

            using (var path = new GraphicsPath())
            {
                int steps = 12;
                PointF[] topCurve = new PointF[steps + 1];
                PointF[] botCurve = new PointF[steps + 1];

                for (int i = 0; i <= steps; i++)
                {
                    float frac = i / (float)steps;
                    float x = poleX + frac * flagW;
                    float wave = isHovered ? (float)(Math.Sin(phase * 6.0f + frac * Math.PI * 2.0) * (3.0f * frac)) : 0f;
                    topCurve[i] = new PointF(x, poleTopY + 2 + wave);
                    botCurve[i] = new PointF(x, poleTopY + 2 + flagH + wave);
                }

                path.AddCurve(topCurve);
                Array.Reverse(botCurve);
                path.AddCurve(botCurve);
                path.CloseFigure();

                using (var flagBrush = new SolidBrush(accent))
                using (var flagPen = new Pen(Color.White, 1f))
                {
                    g.FillPath(flagBrush, path);
                    g.DrawPath(flagPen, path);
                }
            }
        }

        // =========================================================================
        // SNIPER SCOPE OPTICAL ZOOM ANIMATION
        // =========================================================================
        public static void DrawSniperScope(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            // Optical Breathing Zoom Scale
            float zoom = isHovered ? (float)(1.0 + 0.18 * Math.Sin(phase * 4.0f)) : 1.0f;
            float sr = r * zoom;

            // Scope Ring
            using (var scopePen = new Pen(Color.FromArgb(20, 24, 33), 3f))
            using (var retPen = new Pen(Color.FromArgb(239, 68, 68), 1.2f))
            {
                g.DrawEllipse(scopePen, cx - r, cy - r, r * 2f, r * 2f);
                g.DrawLine(retPen, cx - sr, cy, cx + sr, cy);
                g.DrawLine(retPen, cx, cy - sr, cx, cy + sr);
            }

            // Mil-Dot Hashmarks
            using (var dotBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
            {
                for (int d = -3; d <= 3; d++)
                {
                    if (d == 0) continue;
                    float offset = d * (sr * 0.22f);
                    g.FillEllipse(dotBrush, cx + offset - 1f, cy - 1f, 2f, 2f);
                    g.FillEllipse(dotBrush, cx - 1f, cy + offset - 1f, 2f, 2f);
                }
            }

            // Center Point
            using (var centerBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(centerBrush, cx - 1.5f, cy - 1.5f, 3f, 3f);
            }
        }

        // =========================================================================
        // FLAME PULSE ANIMATION (Wildfire / Thermal Hotspot)
        // =========================================================================
        public static void DrawFlamePulse(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.40f;

            // Flame Flicker Displacements
            float f1 = isHovered ? (float)(Math.Sin(phase * 8.0f) * 2.5f) : 0f;
            float f2 = isHovered ? (float)(Math.Cos(phase * 11.0f) * 2.0f) : 0f;

            // Outer Orange Flame
            using (var flamePath = new GraphicsPath())
            {
                flamePath.AddBezier(cx, cy - r - f1, cx + r * 0.7f, cy, cx + r * 0.5f, cy + r * 0.7f, cx, cy + r * 0.7f);
                flamePath.AddBezier(cx, cy + r * 0.7f, cx - r * 0.5f, cy + r * 0.7f, cx - r * 0.7f, cy, cx, cy - r - f1);

                using (var outerBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                {
                    g.FillPath(outerBrush, flamePath);
                }
            }

            // Inner Yellow Core
            using (var corePath = new GraphicsPath())
            {
                corePath.AddBezier(cx, cy - r * 0.5f - f2, cx + r * 0.35f, cy + r * 0.2f, cx + r * 0.25f, cy + r * 0.55f, cx, cy + r * 0.55f);
                corePath.AddBezier(cx, cy + r * 0.55f, cx - r * 0.25f, cy + r * 0.55f, cx - r * 0.35f, cy + r * 0.2f, cx, cy - r * 0.5f - f2);

                using (var coreBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                {
                    g.FillPath(coreBrush, corePath);
                }
            }
        }

        // =========================================================================
        // HELIPAD LZ ANIMATION (Rotor Blade Spin & Perimeter Beacons)
        // =========================================================================
        public static void DrawHelipadLZ(Graphics g, RectangleF bounds, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            // LZ Ring
            using (var lzPen = new Pen(Color.FromArgb(isHovered ? 200 : 120, accent), 1.5f))
            using (var lzBrush = new SolidBrush(Color.FromArgb(140, 15, 23, 42)))
            {
                g.FillEllipse(lzBrush, cx - r, cy - r, r * 2f, r * 2f);
                g.DrawEllipse(lzPen, cx - r, cy - r, r * 2f, r * 2f);
            }

            // Spinning Rotor Blades
            var state = g.Save();
            float rotorAngle = isHovered ? ((phase * 720f) % 360f) : 30f;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(rotorAngle);

            using (var bladePen = new Pen(Color.White, 2f))
            {
                g.DrawLine(bladePen, -r * 0.85f, 0, r * 0.85f, 0);
            }
            g.Restore(state);

            // "H" Helipad Symbol
            using (var hFont = new Font("Arial", 9f, FontStyle.Bold))
            using (var hBrush = new SolidBrush(Color.FromArgb(isHovered ? 255 : 180, Color.White)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("H", hFont, hBrush, cx, cy, sf);
            }

            // 4 Flashing Perimeter Beacons
            Color beaconCol = (isHovered && Math.Sin(phase * 12.0f) > 0) ? Color.FromArgb(245, 158, 11) : Color.FromArgb(100, 245, 158, 11);
            using (var bBrush = new SolidBrush(beaconCol))
            {
                g.FillEllipse(bBrush, cx - 1.5f, cy - r - 1.5f, 3f, 3f);
                g.FillEllipse(bBrush, cx + r - 1.5f, cy - 1.5f, 3f, 3f);
                g.FillEllipse(bBrush, cx - 1.5f, cy + r - 1.5f, 3f, 3f);
                g.FillEllipse(bBrush, cx - r - 1.5f, cy - 1.5f, 3f, 3f);
            }
        }

        // =========================================================================
        // GENERAL PULSING / ROTATING / BOUNCING HELPERS
        // =========================================================================
        public static void DrawPulsingIcon(Graphics g, RectangleF bounds, string iconKey, Color accent, float phase, bool isHovered)
        {
            float scale = isHovered ? (float)(1.0 + 0.20 * Math.Sin(phase * 5.0f)) : 1.0f;
            DrawStaticIcon(g, bounds, iconKey, accent, scale);
        }

        public static void DrawRotatingIcon(Graphics g, RectangleF bounds, string iconKey, Color accent, float phase, bool isHovered)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            var state = g.Save();

            if (isHovered)
            {
                float angle = (phase * 150f) % 360f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);
                g.TranslateTransform(-cx, -cy);
            }

            DrawStaticIcon(g, bounds, iconKey, accent, isHovered ? 1.15f : 1.0f);
            g.Restore(state);
        }

        public static void DrawBouncingIcon(Graphics g, RectangleF bounds, string iconKey, Color accent, float phase, bool isHovered)
        {
            var newBounds = bounds;
            if (isHovered)
            {
                float dy = (float)(Math.Sin(phase * 6.0f) * 3.0);
                newBounds.Y += dy;
            }
            DrawStaticIcon(g, newBounds, iconKey, accent, isHovered ? 1.15f : 1.0f);
        }

        // =========================================================================
        // STATIC / VECTOR DRAWING FALLBACK
        // =========================================================================
        public static void DrawStaticIcon(Graphics g, RectangleF bounds, string iconKey, Color accent, float scale = 1.0f)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.38f * scale;

            string key = (iconKey ?? "").ToLowerInvariant();

            if (key.Contains("goto") && key.Contains("elev"))
            {
                DrawGotoElevation(g, bounds, accent, 0f, false);
                return;
            }
            if (key.Contains("goto") || key.Contains("bear") || key.Contains("compass"))
            {
                DrawGotoBearing(g, bounds, accent, 0f, false);
                return;
            }

            switch (key)
            {
                case "crosshair":
                case "target":
                    using (var pen = new Pen(accent, 1.8f))
                    using (var wPen = new Pen(Color.White, 1.2f))
                    {
                        g.DrawEllipse(pen, cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f);
                        g.DrawLine(wPen, cx - r, cy, cx - r * 0.3f, cy);
                        g.DrawLine(wPen, cx + r * 0.3f, cy, cx + r, cy);
                        g.DrawLine(wPen, cx, cy - r, cx, cy - r * 0.3f);
                        g.DrawLine(wPen, cx, cy + r * 0.3f, cx, cy + r);
                        g.FillEllipse(new SolidBrush(Color.White), cx - 1.5f, cy - 1.5f, 3f, 3f);
                    }
                    break;

                case "camera_ptz":
                case "camera":
                    using (var pen = new Pen(accent, 1.6f))
                    using (var fillBrush = new SolidBrush(Color.FromArgb(200, 30, 41, 59)))
                    {
                        g.FillEllipse(fillBrush, cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f);
                        g.DrawEllipse(pen, cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f);
                        g.FillPolygon(new SolidBrush(accent), new[] {
                            new PointF(cx - 3, cy - r * 0.5f),
                            new PointF(cx + 3, cy - r * 0.5f),
                            new PointF(cx, cy - r * 1.0f)
                        });
                        g.FillEllipse(new SolidBrush(Color.White), cx - 2, cy - 2, 4, 4);
                    }
                    break;

                case "drone":
                case "uav":
                    using (var pen = new Pen(Color.White, 1.8f))
                    using (var rBrush = new SolidBrush(Color.FromArgb(180, accent)))
                    {
                        g.DrawLine(pen, cx - r * 0.7f, cy - r * 0.7f, cx + r * 0.7f, cy + r * 0.7f);
                        g.DrawLine(pen, cx - r * 0.7f, cy + r * 0.7f, cx + r * 0.7f, cy - r * 0.7f);
                        g.FillEllipse(rBrush, cx - r * 0.9f, cy - r * 0.9f, r * 0.4f, r * 0.4f);
                        g.FillEllipse(rBrush, cx + r * 0.5f, cy - r * 0.9f, r * 0.4f, r * 0.4f);
                        g.FillEllipse(rBrush, cx - r * 0.9f, cy + r * 0.5f, r * 0.4f, r * 0.4f);
                        g.FillEllipse(rBrush, cx + r * 0.5f, cy + r * 0.5f, r * 0.4f, r * 0.4f);
                        g.FillEllipse(new SolidBrush(Color.White), cx - 2f, cy - 2f, 4f, 4f);
                    }
                    break;

                case "threat":
                case "warning":
                    using (var pen = new Pen(Color.White, 1.4f))
                    using (var fBrush = new SolidBrush(Color.FromArgb(220, 239, 68, 68)))
                    {
                        PointF[] tri = {
                            new PointF(cx, cy - r),
                            new PointF(cx + r * 0.9f, cy + r * 0.8f),
                            new PointF(cx - r * 0.9f, cy + r * 0.8f)
                        };
                        g.FillPolygon(fBrush, tri);
                        g.DrawPolygon(pen, tri);
                        using (var wPen = new Pen(Color.White, 1.6f))
                        {
                            g.DrawLine(wPen, cx, cy - r * 0.3f, cx, cy + r * 0.2f);
                            g.FillEllipse(new SolidBrush(Color.White), cx - 1f, cy + r * 0.45f, 2f, 2f);
                        }
                    }
                    break;

                case "satellite":
                case "satcom":
                    DrawOrbitSatellite(g, bounds, accent, 0f, false);
                    break;

                case "shield":
                case "defense":
                    DrawShieldDefense(g, bounds, accent, 0f, false);
                    break;

                case "missile":
                case "rocket":
                    DrawMissileTrajectory(g, bounds, accent, 0f, false);
                    break;

                case "aircraft":
                case "plane":
                    DrawFlightHorizon(g, bounds, accent, 0f, false);
                    break;

                case "vessel":
                case "ship":
                    DrawMaritimeWake(g, bounds, accent, 0f, false);
                    break;

                case "waypoint":
                case "flag":
                    DrawFlagWaving(g, bounds, accent, 0f, false);
                    break;

                case "sniper":
                    DrawSniperScope(g, bounds, accent, 0f, false);
                    break;

                case "fire":
                case "flame":
                    DrawFlamePulse(g, bounds, accent, 0f, false);
                    break;

                case "helipad":
                    DrawHelipadLZ(g, bounds, accent, 0f, false);
                    break;

                case "edit":
                case "rename":
                    using (var pen = new Pen(accent, 1.8f))
                    using (var wPen = new Pen(Color.White, 1.3f))
                    {
                        // Angled pencil body
                        g.DrawLine(pen, cx - r * 0.6f, cy + r * 0.4f, cx + r * 0.3f, cy - r * 0.5f);
                        g.DrawLine(pen, cx - r * 0.3f, cy + r * 0.7f, cx + r * 0.6f, cy - r * 0.2f);
                        g.DrawLine(wPen, cx + r * 0.3f, cy - r * 0.5f, cx + r * 0.6f, cy - r * 0.2f);
                        // Tip
                        PointF[] tip = {
                            new PointF(cx - r * 0.6f, cy + r * 0.4f),
                            new PointF(cx - r * 0.3f, cy + r * 0.7f),
                            new PointF(cx - r * 0.8f, cy + r * 0.8f)
                        };
                        g.FillPolygon(new SolidBrush(Color.White), tip);
                        // Bottom edit underline
                        g.DrawLine(wPen, cx - r * 0.8f, cy + r * 0.95f, cx + r * 0.8f, cy + r * 0.95f);
                    }
                    break;

                case "trash":
                case "delete":
                case "remove":
                    using (var pen = new Pen(accent, 1.6f))
                    using (var wPen = new Pen(Color.White, 1.4f))
                    {
                        // Bin body
                        PointF[] body = {
                            new PointF(cx - r * 0.5f, cy - r * 0.2f),
                            new PointF(cx - r * 0.4f, cy + r * 0.8f),
                            new PointF(cx + r * 0.4f, cy + r * 0.8f),
                            new PointF(cx + r * 0.5f, cy - r * 0.2f)
                        };
                        g.DrawPolygon(pen, body);
                        // Bin vertical ribs
                        g.DrawLine(pen, cx - r * 0.18f, cy, cx - r * 0.15f, cy + r * 0.6f);
                        g.DrawLine(pen, cx + r * 0.18f, cy, cx + r * 0.15f, cy + r * 0.6f);
                        // Lid
                        g.DrawLine(wPen, cx - r * 0.7f, cy - r * 0.3f, cx + r * 0.7f, cy - r * 0.3f);
                        // Handle
                        g.DrawLine(wPen, cx - r * 0.25f, cy - r * 0.5f, cx + r * 0.25f, cy - r * 0.5f);
                        g.DrawLine(wPen, cx - r * 0.25f, cy - r * 0.5f, cx - r * 0.25f, cy - r * 0.3f);
                        g.DrawLine(wPen, cx + r * 0.25f, cy - r * 0.5f, cx + r * 0.25f, cy - r * 0.3f);
                    }
                    break;

                case "pin":
                case "marker":
                default:
                    using (var brush = new SolidBrush(accent))
                    using (var pen = new Pen(Color.White, 1.4f))
                    using (var path = new GraphicsPath())
                    {
                        path.AddArc(cx - r * 0.6f, cy - r * 0.9f, r * 1.2f, r * 1.2f, 180, 180);
                        path.AddLine(cx + r * 0.6f, cy - r * 0.3f, cx, cy + r * 0.8f);
                        path.AddLine(cx, cy + r * 0.8f, cx - r * 0.6f, cy - r * 0.3f);
                        path.CloseFigure();
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                        g.FillEllipse(new SolidBrush(Color.FromArgb(20, 24, 33)), cx - 2.5f, cy - r * 0.4f, 5f, 5f);
                    }
                    break;
            }
        }
    }
}
