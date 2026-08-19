using System;
using System.Diagnostics;
using System.IO;

namespace Mapsui48.Client
{
    internal class MapsuiHostProcess : IDisposable
    {
        private Process _process;
        public string PipeName { get; }

        public MapsuiHostProcess()
        {
            PipeName = "Mapsui48_" + Guid.NewGuid().ToString("N");
        }

        public void Start(string hostExePath = null)
        {
            if (string.IsNullOrEmpty(hostExePath))
            {
                // Default to Host subfolder
                hostExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Host", "Mapsui48.Host.exe");
                
                // Fallback 1: Same directory
                if (!File.Exists(hostExePath))
                    hostExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mapsui48.Host.exe");

                // Fallback 2: Relative to demo build directory
                if (!File.Exists(hostExePath))
                {
                    string rel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Mapsui48.Demo", "bin", "Debug", "Host", "Mapsui48.Host.exe");
                    if (File.Exists(rel)) hostExePath = Path.GetFullPath(rel);
                }
            }

            if (!File.Exists(hostExePath))
                throw new FileNotFoundException($"Host executable not found at {hostExePath}. Please ensure the Host directory is copied to your output folder.");

            var startInfo = new ProcessStartInfo
            {
                FileName = hostExePath,
                Arguments = $"--pipe-name {PipeName}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(startInfo);
        }

        public bool IsRunning => _process != null && !_process.HasExited;

        public void Stop()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(1000);
                }
            }
            catch
            {
                // Ignore kill errors during shutdown
            }
            finally
            {
                _process?.Dispose();
                _process = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
