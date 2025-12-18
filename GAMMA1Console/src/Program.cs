/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    GAMMA1 CONSOLE - MAIN ENTRY POINT                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Professional-grade control interface for Project NIGHTFRAME               ║
 * ║  Version: 1.0.0                                                            ║
 * ║  Classification: Research & Development                                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;

namespace GAMMA1Console
{
    static class Program
    {
        private static Mutex? _mutex;
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME", "logs");

        [STAThread]
        static void Main(string[] args)
        {
            // Ensure single instance
            const string mutexName = "NIGHTFRAME_GAMMA1_CONSOLE";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show(
                    "GAMMA1 Console is already running.",
                    "NIGHTFRAME",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Initialize logging
            InitializeLogging();
            Log("═══════════════════════════════════════════════════════════════");
            Log("GAMMA1 Console starting...");
            Log($"Version: 1.0.0");
            Log($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"User: {Environment.UserName}");
            Log($"Machine: {Environment.MachineName}");
            Log("═══════════════════════════════════════════════════════════════");

            // Global exception handling
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Application configuration
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            try
            {
                // Check for command line arguments
                bool nodeMode = false;
                string? centerAddress = null;
                string? nodeId = null;

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--node-mode")
                        nodeMode = true;
                    else if (args[i].StartsWith("--center="))
                        centerAddress = args[i].Substring(9);
                    else if (args[i].StartsWith("--node-id="))
                        nodeId = args[i].Substring(10);
                }

                if (nodeMode && !string.IsNullOrEmpty(centerAddress))
                {
                    Log($"Starting in NODE mode - Center: {centerAddress}");
                    // Node mode initialization would go here
                }

                // Launch main form
                Application.Run(new MainConsoleForm());
                
                Log("GAMMA1 Console shutting down normally.");
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex}");
                MessageBox.Show(
                    $"A fatal error occurred:\n\n{ex.Message}\n\nSee log for details.",
                    "NIGHTFRAME - Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }

        private static void InitializeLogging()
        {
            try
            {
                Directory.CreateDirectory(LogPath);
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                var logFile = Path.Combine(LogPath, $"gamma1_{DateTime.Now:yyyyMMdd}.log");
                var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                File.AppendAllText(logFile, entry + Environment.NewLine);
                Console.WriteLine(entry);
            }
            catch { }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log($"THREAD EXCEPTION: {e.Exception}");
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}",
                "NIGHTFRAME - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log($"UNHANDLED EXCEPTION: {ex}");
            
            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex?.Message}\n\nThe application will now close.",
                    "NIGHTFRAME - Critical Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
