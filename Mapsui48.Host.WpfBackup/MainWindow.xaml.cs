using Mapsui48.Host.Services;
using System.Windows;

namespace Mapsui48.Host;

public partial class MainWindow : Window
{
    private readonly MapService _mapService;
    private readonly CommandDispatcher _dispatcher;
    private readonly PipeServer _pipeServer;

    public MainWindow(string pipeName)
    {
        InitializeComponent();

        // 1. Initialize MapService
        _mapService = new MapService(MapControl, SendEvent);

        // 2. Initialize Command Dispatcher
        _dispatcher = new CommandDispatcher(_mapService, this);

        // 3. Start Pipe Server
        _pipeServer = new PipeServer(pipeName, _dispatcher);
        _pipeServer.Start();
    }

    private void SendEvent(Protocol.MapEvent evt)
    {
        _pipeServer?.SendEvent(evt);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _pipeServer?.Dispose();
        base.OnClosed(e);
    }
}