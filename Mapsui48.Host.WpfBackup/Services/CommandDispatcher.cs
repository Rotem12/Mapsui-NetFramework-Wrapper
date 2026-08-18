using Mapsui48.Host.Embedding;
using Mapsui48.Protocol;
using System;
using System.Text.Json;
using System.Windows;

namespace Mapsui48.Host.Services
{
    public class CommandDispatcher
    {
        private readonly MapService _mapService;
        private readonly Window _mainWindow;

        public CommandDispatcher(MapService mapService, Window mainWindow)
        {
            _mapService = mapService;
            _mainWindow = mainWindow;
        }

        public MapResponse Dispatch(string jsonCommand)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonCommand);
                var type = doc.RootElement.GetProperty("Type").GetString();
                var id = doc.RootElement.GetProperty("Id").GetString();

                var response = new MapResponse { Id = id, Success = true };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        switch (type)
                        {
                            case "Ping":
                                break;
                            
                            case "AttachTo":
                                var attachCmd = JsonSerializer.Deserialize<AttachToCommand>(jsonCommand);
                                WindowEmbedder.AttachTo(_mainWindow, new IntPtr(attachCmd.ParentHwnd));
                                _mainWindow.Show(); // Make it visible after reparenting
                                break;

                            case "SetTileSource":
                                var tileCmd = JsonSerializer.Deserialize<SetTileSourceCommand>(jsonCommand);
                                _mapService.SetTileSource(tileCmd.MBTilesPath, tileCmd.OnlineUrl, tileCmd.CachePath);
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
                                var polyId = _mapService.AddPolygon(polyCmd);
                                response.Data = JsonSerializer.Serialize(new { FeatureId = polyId });
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
