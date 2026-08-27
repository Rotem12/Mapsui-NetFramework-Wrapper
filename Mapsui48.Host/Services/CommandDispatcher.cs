using Mapsui48.Host.Embedding;
using Mapsui48.Protocol;
using System;
using System.Text.Json;
using System.Windows.Forms;

namespace Mapsui48.Host.Services
{
    public class CommandDispatcher
    {
        private readonly MapService _mapService;
        private readonly Form _mainForm;

        public CommandDispatcher(MapService mapService, Form mainForm)
        {
            _mapService = mapService;
            _mainForm = mainForm;
        }

        public MapResponse Dispatch(string jsonCommand)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonCommand);
                var type = doc.RootElement.GetProperty("Type").GetString();
                var id = doc.RootElement.GetProperty("Id").GetString();

                var response = new MapResponse { Id = id, Success = true };

                _mainForm.Invoke((MethodInvoker)delegate
                {
                    try
                    {
                        switch (type)
                        {
                            case "Ping":
                                break;
                            
                            case "AttachTo":
                                var attachCmd = JsonSerializer.Deserialize<AttachToCommand>(jsonCommand);
                                WindowEmbedder.AttachTo(_mainForm, new IntPtr(attachCmd.ParentHwnd));
                                _mainForm.Show(); // Make it visible after reparenting
                                ((MapForm)_mainForm).AttachMapControl(); // Add MapControl now that parent HWND is set
                                break;

                            case "SetTileSource":
                                var tileCmd = JsonSerializer.Deserialize<SetTileSourceCommand>(jsonCommand);
                                _mapService.SetTileSource(tileCmd.MBTilesPath, tileCmd.OnlineUrl, tileCmd.CachePath);
                                break;

                            case "LoadVectorTile":
                                var vtCmd = JsonSerializer.Deserialize<LoadVectorTileCommand>(jsonCommand);
                                _mapService.SetTileSource(vtCmd.MBTilesPath, null, null);
                                break;

                            case "NavigateTo":
                                var navCmd = JsonSerializer.Deserialize<NavigateToCommand>(jsonCommand);
                                _mapService.NavigateTo(navCmd.Latitude, navCmd.Longitude, navCmd.ZoomLevel, navCmd.DurationMs);
                                break;

                            case "FlyTo":
                                var flyCmd = JsonSerializer.Deserialize<FlyToCommand>(jsonCommand);
                                _mapService.FlyTo(flyCmd.Latitude, flyCmd.Longitude, flyCmd.ZoomLevel, flyCmd.DurationMs);
                                break;

                            case "SetZoom":
                                var zoomCmd = JsonSerializer.Deserialize<SetZoomCommand>(jsonCommand);
                                _mapService.SetZoom(zoomCmd.ZoomLevel, zoomCmd.DurationMs);
                                break;

                            case "GoHome":
                                var homeCmd = JsonSerializer.Deserialize<GoHomeCommand>(jsonCommand);
                                _mapService.GoHome(homeCmd.DurationMs);
                                break;

                            case "AddPolygon":
                                var polyCmd = JsonSerializer.Deserialize<AddPolygonCommand>(jsonCommand);
                                var polyId = polyCmd != null ? _mapService.AddPolygon(polyCmd) : "";
                                response.Data = JsonSerializer.Serialize(new { FeatureId = polyId });
                                break;

                            case "AddCircle":
                                var circleCmd = JsonSerializer.Deserialize<AddCircleCommand>(jsonCommand);
                                var circleId = circleCmd != null ? _mapService.AddCircle(circleCmd) : "";
                                response.Data = JsonSerializer.Serialize(new { FeatureId = circleId });
                                break;

                            case "AddPoint":

                                var ptCmd = JsonSerializer.Deserialize<AddPointCommand>(jsonCommand);
                                var ptId = _mapService.AddPoint(ptCmd);
                                response.Data = JsonSerializer.Serialize(new { FeatureId = ptId });
                                break;

                            case "AddLine":
                                var lineCmd = JsonSerializer.Deserialize<AddLineCommand>(jsonCommand);
                                var lineId = _mapService.AddLine(lineCmd);
                                response.Data = JsonSerializer.Serialize(new { FeatureId = lineId });
                                break;

                            case "RemoveFeature":
                                var remCmd = JsonSerializer.Deserialize<RemoveFeatureCommand>(jsonCommand);
                                _mapService.RemoveFeature(remCmd.LayerName, remCmd.FeatureId);
                                break;

                            case "ClearLayer":
                                var clrCmd = JsonSerializer.Deserialize<ClearLayerCommand>(jsonCommand);
                                _mapService.ClearLayer(clrCmd.LayerName);
                                break;

                            // ── Viewport & Navigation ────────────────────

                            case "RotateTo":
                                var rotCmd = JsonSerializer.Deserialize<RotateToCommand>(jsonCommand);
                                _mapService.RotateTo(rotCmd.Heading, rotCmd.DurationMs, rotCmd.Easing);
                                break;

                            case "SetRotationLock":
                                var rotLockCmd = JsonSerializer.Deserialize<SetRotationLockCommand>(jsonCommand);
                                _mapService.SetRotationLock(rotLockCmd.Locked);
                                break;

                            case "ZoomToBox":
                                var boxCmd = JsonSerializer.Deserialize<ZoomToBoxCommand>(jsonCommand);
                                _mapService.ZoomToBox(boxCmd.MinLat, boxCmd.MinLon, boxCmd.MaxLat, boxCmd.MaxLon, boxCmd.DurationMs, boxCmd.BoxFit);
                                break;

                            case "SetViewportBounds":
                                var boundsCmd = JsonSerializer.Deserialize<SetViewportBoundsCommand>(jsonCommand);
                                _mapService.SetViewportBounds(boundsCmd.MinLat, boundsCmd.MinLon, boundsCmd.MaxLat, boundsCmd.MaxLon, boundsCmd.MinZoom, boundsCmd.MaxZoom);
                                break;

                            case "SetPanLock":
                                var panLockCmd = JsonSerializer.Deserialize<SetPanLockCommand>(jsonCommand);
                                _mapService.SetPanLock(panLockCmd.Locked);
                                break;

                            case "SetZoomLock":
                                var zoomLockCmd = JsonSerializer.Deserialize<SetZoomLockCommand>(jsonCommand);
                                _mapService.SetZoomLock(zoomLockCmd.Locked);
                                break;

                            // ── Layer Management ─────────────────────────

                            case "SetLayerVisibility":
                                var visCmd = JsonSerializer.Deserialize<SetLayerVisibilityCommand>(jsonCommand);
                                _mapService.SetLayerVisibility(visCmd.LayerName, visCmd.Visible);
                                break;

                            case "SetLayerOpacity":
                                var opCmd = JsonSerializer.Deserialize<SetLayerOpacityCommand>(jsonCommand);
                                _mapService.SetLayerOpacity(opCmd.LayerName, opCmd.Opacity);
                                break;

                            case "SetLayerScaleRange":
                                var srCmd = JsonSerializer.Deserialize<SetLayerScaleRangeCommand>(jsonCommand);
                                _mapService.SetLayerScaleRange(srCmd.LayerName, srCmd.MinZoom, srCmd.MaxZoom);
                                break;

                            case "RemoveLayer":
                                var remLayerCmd = JsonSerializer.Deserialize<RemoveLayerCommand>(jsonCommand);
                                _mapService.RemoveLayer(remLayerCmd.LayerName);
                                break;

                            case "GetLayers":
                                var layersList = _mapService.GetLayers();
                                response.Data = JsonSerializer.Serialize(layersList);
                                break;

                            // ── Batch & Advanced Features ────────────────

                            case "AddFeaturesBatch":
                                var batchCmd = JsonSerializer.Deserialize<AddFeaturesBatchCommand>(jsonCommand);
                                var batchIds = _mapService.AddFeaturesBatch(batchCmd);
                                response.Data = JsonSerializer.Serialize(new { FeatureIds = batchIds });
                                break;

                            case "UpdateFeature":
                                var updCmd = JsonSerializer.Deserialize<UpdateFeatureCommand>(jsonCommand);
                                _mapService.UpdateFeature(updCmd);
                                break;

                            case "ShowCallout":
                                var calloutCmd = JsonSerializer.Deserialize<ShowCalloutCommand>(jsonCommand);
                                _mapService.ShowCallout(calloutCmd);
                                break;

                            // ── Canvas HUD Widgets ───────────────────────

                            case "SetScaleBarWidget":
                                var sbCmd = JsonSerializer.Deserialize<SetScaleBarWidgetCommand>(jsonCommand);
                                _mapService.SetScaleBarWidget(sbCmd.Enabled, sbCmd.Position, sbCmd.Mode);
                                break;

                            case "SetMouseCoordinatesWidget":
                                var mcCmd = JsonSerializer.Deserialize<SetMouseCoordinatesWidgetCommand>(jsonCommand);
                                _mapService.SetMouseCoordinatesWidget(mcCmd.Enabled, mcCmd.Position);
                                break;

                            case "SetPerformanceWidget":
                                var pwCmd = JsonSerializer.Deserialize<SetPerformanceWidgetCommand>(jsonCommand);
                                _mapService.SetPerformanceWidget(pwCmd.Enabled, pwCmd.Position);
                                break;

                            case "SetZoomButtonsWidget":
                                var zbCmd = JsonSerializer.Deserialize<SetZoomButtonsWidgetCommand>(jsonCommand);
                                _mapService.SetZoomButtonsWidget(zbCmd.Enabled, zbCmd.Position);
                                break;

                            // ── Snapshot & Utilities ─────────────────────

                            case "GetSnapshot":
                                {
                                    var snapCmd = JsonSerializer.Deserialize<GetSnapshotCommand>(jsonCommand);
                                    var snapData = _mapService.GetSnapshot(snapCmd?.Format ?? "Png", snapCmd?.Quality ?? 100);
                                    response.Data = JsonSerializer.Serialize(new { Base64Image = snapData });
                                    break;
                                }

                            // ── GIS Data Loaders & Formats ────────────────

                            case "LoadGeoJson":
                                {
                                    var geojsonCmd = JsonSerializer.Deserialize<LoadGeoJsonCommand>(jsonCommand);
                                    if (geojsonCmd != null) _mapService.LoadGeoJson(geojsonCmd);
                                    break;
                                }

                            case "LoadShapefile":
                                {
                                    var shpCmd = JsonSerializer.Deserialize<LoadShapefileCommand>(jsonCommand);
                                    if (shpCmd != null) _mapService.LoadShapefile(shpCmd);
                                    break;
                                }

                            case "AddWmsLayer":
                                {
                                    var wmsCmd = JsonSerializer.Deserialize<AddWmsLayerCommand>(jsonCommand);
                                    if (wmsCmd != null) _mapService.AddWmsLayer(wmsCmd);
                                    break;
                                }

                            // ── Coordinate Translation & Spatial Queries ──

                            case "ScreenToWorld":
                                {
                                    var s2wCmd = JsonSerializer.Deserialize<ScreenToWorldCommand>(jsonCommand);
                                    var coordResult = _mapService.ScreenToWorld(s2wCmd?.ScreenX ?? 0, s2wCmd?.ScreenY ?? 0);
                                    response.Data = JsonSerializer.Serialize(coordResult);
                                    break;
                                }

                            case "WorldToScreen":
                                {
                                    var w2sCmd = JsonSerializer.Deserialize<WorldToScreenCommand>(jsonCommand);
                                    var screenResult = _mapService.WorldToScreen(w2sCmd?.Latitude ?? 0, w2sCmd?.Longitude ?? 0);
                                    response.Data = JsonSerializer.Serialize(screenResult);
                                    break;
                                }

                            case "GetLayerBounds":
                                {
                                    var getLayerBoundsCmd = JsonSerializer.Deserialize<GetLayerBoundsCommand>(jsonCommand);
                                    var boundsResult = _mapService.GetLayerBounds(getLayerBoundsCmd?.LayerName ?? "");
                                    response.Data = JsonSerializer.Serialize(boundsResult);
                                    break;
                                }


                            // ── Measurement & Ruler Widget ───────────────

                            case "SetRulerWidget":
                                {
                                    var rulerCmd = JsonSerializer.Deserialize<SetRulerWidgetCommand>(jsonCommand);
                                    _mapService.SetRulerWidget(rulerCmd?.Enabled ?? false);
                                    break;
                                }

                            // ── Animated Glide Tracking ──────────────────

                            case "AddAnimatedPoint":
                                {
                                    var animPointCmd = JsonSerializer.Deserialize<AddAnimatedPointCommand>(jsonCommand);
                                    var animId = animPointCmd != null ? _mapService.AddAnimatedPoint(animPointCmd) : "";
                                    response.Data = JsonSerializer.Serialize(new { FeatureId = animId });
                                    break;
                                }

                            case "UpdateAnimatedPoint":
                                {
                                    var updAnimCmd = JsonSerializer.Deserialize<UpdateAnimatedPointCommand>(jsonCommand);
                                    if (updAnimCmd != null) _mapService.UpdateAnimatedPoint(updAnimCmd);
                                    break;
                                }

                            // ── Mouse & Pointer Events ───────────────────

                            case "SetPointerMoveEvents":
                                {
                                    var ptrCmd = JsonSerializer.Deserialize<SetPointerMoveEventsCommand>(jsonCommand);
                                    _mapService.SetPointerMoveEvents(ptrCmd?.Enabled ?? false);
                                    break;
                                }

                            default:
                                response.Success = false;
                                response.Error = $"Unknown command type: {type}";
                                break;



                        }
                    }
                    catch (Exception ex)
                    {
                        response.Success = false;
                        response.Error = ex.Message;
                    }
                });

                return response;
            }
            catch (Exception ex)
            {
                return new MapResponse { Success = false, Error = "Failed to parse command: " + ex.Message };
            }
        }
    }
}
