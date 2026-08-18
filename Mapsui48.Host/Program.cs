using System;
using System.Windows.Forms;

namespace Mapsui48.Host
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            string pipeName = "Mapsui48_TestPipe"; // default for debugging
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--pipe-name" && i + 1 < args.Length)
                {
                    pipeName = args[i + 1];
                    break;
                }
            }
            
            System.IO.File.AppendAllText("host_startup.log", $"Starting Host with pipe: {pipeName}\n");

            try
            {
                Application.Run(new MapForm(pipeName));
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("host_startup.log", $"Crash: {ex}\n");
            }
        }
    }
}
