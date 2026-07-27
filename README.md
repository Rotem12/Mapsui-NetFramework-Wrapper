# Mapsui-NetFramework-Wrapper

**Mapsui-NetFramework-Wrapper** is a high-performance inter-process communication (IPC) wrapper that allows you to seamlessly embed modern [Mapsui](https://github.com/Mapsui/Mapsui) (running on .NET 8) inside legacy .NET Framework (WinForms) applications.

Due to Mapsui 4.x and 5.x migrating to .NET Standard 2.0 / .NET 6+ and utilizing modern SkiaSharp rendering, many older .NET Framework applications (like .NET 4.8 WinForms apps) were left unable to easily upgrade without suffering from dependency conflicts or complete rewrites. 

This repository solves that problem by using an **Out-Of-Process (OOP)** architectural approach.

## 🌟 How It Works

Instead of trying to force .NET 8 DLLs into a .NET 4.8 AppDomain, the wrapper splits the UI into two processes:
1. **The Client (WinForms .NET 4.8)**: Your legacy application. It acts as a lightweight controller.
2. **The Host (WPF .NET 8)**: A hidden, headless process that loads the modern Mapsui MapControl, leverages hardware-accelerated Skia rendering, and natively uses .NET 8.

When you drop the `MapHostPanel` control onto your WinForms app:
* The Client automatically spins up the .NET 8 Host process in the background.
* Using Win32 API (`SetParent`), it strips the window chrome off the WPF Host and seamlessly anchors it directly *inside* your WinForms panel. 
* A lightning-fast **Named Pipe Stream** is established to ferry JSON commands (like navigating, adding polygons, adding markers) and events (like clicks and viewport changes) back and forth asynchronously.

To the end user, it looks and feels exactly like a native WinForms Mapsui control!

## 📦 Project Structure

* `Mapsui48.Demo` (.NET 4.8): A sample WinForms application demonstrating how to use the `MapHostPanel`.
* `Mapsui48.Client` (.NET 4.8): The client-side library containing the WinForms panel wrapper and the IPC pipe client.
* `Mapsui48.Host` (.NET 8 WPF): The map rendering engine. It outputs directly to a `Host` subfolder to avoid polluting the .NET Framework directory with .NET Core DLLs.
* `Mapsui48.Protocol` (.NET Standard 2.0): The shared JSON contract models governing IPC communication.
* `maperitive_parallel.ps1` & `generate_mbtiles.bat`: Multi-core parallel raster MBTiles generator for high-performance offline maps.

## 🚀 Features

* **Zero Dependency Hell**: The Host builds to its own isolated `Host/` subfolder, ensuring your .NET Framework app never crashes from loading the wrong version of `System.Text.Json` or `SkiaSharp`.
* **Hybrid Tile Caching**: Supports `MBTiles` for full offline support, standard OSM XYZ tile servers, and automatic local caching of online tiles.
* **Vector Graphics**: Supports drawing Polygons, Points, and Lines directly onto the map via the WinForms API.
* **Bi-directional Events**: Captures `MapClicked`, `FeatureClicked`, and `ViewportChanged` events natively in WinForms.
* **64-bit Safe**: Built entirely on `IntPtr` and thread-safe Task arrays to fully support modern Windows x64.
* **High-Octane Parallel Tile Generation**: Includes a PowerShell script that splits Maperitive rendering into spatial 2D grids across CPU cores for 10x-15x faster offline MBTiles creation.

## 🛠️ Setup & Installation

### Requirements
* [Visual Studio 2022](https://visualstudio.microsoft.com/)
* .NET 8 SDK
* .NET Framework 4.8 Developer Pack

### Building the Solution
1. Clone the repository: `git clone https://github.com/Rotem12/Mapsui-NetFramework-Wrapper.git`
2. Open `Mapsui48.slnx` (or `Mapsui48.sln`) in Visual Studio.
3. Set **Mapsui48.Demo** as the Startup Project.
4. Hit **F5** (or `dotnet build` from the CLI).

*Note: The MSBuild scripts are already configured to perfectly route the output directories so `Mapsui48.Host.exe` securely drops into the `bin/Debug/Host/` subfolder, keeping your runtime environment clean.*

## 🗺️ Offline MBTiles Parallel Generator

This repository includes a load-balanced 2D spatial grid generator (`maperitive_parallel.ps1`) for generating offline `.mbtiles` maps from OpenStreetMap `.pbf` extracts:

```cmd
generate_mbtiles.bat
```
* **Auto BBox Detection**: Parses the PBF header to extract exact geographical bounds.
* **Spatial Grid Splitting**: Automatically divides max-zoom rendering across CPU cores for maximum speedup.
* **Automatic SQLite Merge**: Combines all tile parts into a single consolidated `.mbtiles` file ready to use.

## 💻 Code Example

Adding a map to your .NET 4.8 WinForms app is as simple as adding any other control:

```csharp
using Mapsui48.Client;

public partial class MainForm : Form
{
    private MapHostPanel _mapPanel;

    public MainForm()
    {
        InitializeComponent();
        
        _mapPanel = new MapHostPanel
        {
            Dock = DockStyle.Fill,
            // Example of Offline MBTiles
            // MBTilesPath = @"C:\path\to\offline.mbtiles",
            
            // Example of Online OSM Tiles with local caching
            OnlineUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
            CachePath = @"C:\AppCache\MapTiles"
        };

        // Listen for events
        _mapPanel.MapClicked += (s, e) => Console.WriteLine($"Clicked: {e.Latitude}, {e.Longitude}");
        _mapPanel.ViewportChanged += (s, e) => lblStatus.Text = $"Zoom: {e.ZoomLevel}";

        // Add to your UI
        this.Controls.Add(_mapPanel);
    }
    
    private async void btnFly_Click(object sender, EventArgs e)
    {
        // Smoothly navigate home
        await _mapPanel.GoHomeAsync(durationMs: 2000);
    }
}
```

## 📜 License

MIT License. Feel free to use this wrapper in both open-source and commercial WinForms applications.
