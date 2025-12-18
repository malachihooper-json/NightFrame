/*
 * AGENT 3 - STEALTH OPERATIONS
 * Covert node behavior: task manager invisibility, surveillance blindness
 */
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Network
{
    /// <summary>
    /// Provides stealth capabilities for nodes to operate covertly.
    /// </summary>
    public class StealthOperations
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        
        private readonly Process _currentProcess;
        private bool _stealthMode = false;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public bool IsStealthMode => _stealthMode;
        
        public StealthOperations()
        {
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// Enables stealth mode - reduces visibility in task management.
        /// </summary>
        public void EnableStealthMode()
        {
            _stealthMode = true;
            
            // Hide console window if present
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
            
            // Set process priority to below normal to avoid detection
            try
            {
                _currentProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch { }
            
            EmitThought("◎ Stealth mode enabled");
        }

        /// <summary>
        /// Disables stealth mode.
        /// </summary>
        public void DisableStealthMode()
        {
            _stealthMode = false;
            
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_SHOW);
            }
            
            try
            {
                _currentProcess.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch { }
            
            EmitThought("◎ Stealth mode disabled");
        }

        /// <summary>
        /// Performs network operation with surveillance evasion.
        /// Uses randomized timing and indirect connections.
        /// </summary>
        public async Task<T?> ExecuteCovertlyAsync<T>(Func<Task<T>> operation)
        {
            // Random delay to avoid pattern detection
            var delay = new Random().Next(500, 3000);
            await Task.Delay(delay);
            
            try
            {
                return await operation();
            }
            catch
            {
                // Fail silently in stealth mode
                return default;
            }
        }

        /// <summary>
        /// Throttles CPU usage to avoid detection.
        /// </summary>
        public async Task ThrottledExecuteAsync(Action action, int maxCpuPercent = 30)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            
            // Calculate sleep time to maintain target CPU percentage
            var sleepTime = (int)(sw.ElapsedMilliseconds * (100 - maxCpuPercent) / maxCpuPercent);
            if (sleepTime > 0)
            {
                await Task.Delay(sleepTime);
            }
        }

        /// <summary>
        /// Randomizes network request timing to evade surveillance.
        /// </summary>
        public async Task RandomizedDelayAsync()
        {
            var delay = new Random().Next(1000, 10000);
            await Task.Delay(delay);
        }

        /// <summary>
        /// Obfuscates the origin of the node to prevent identification.
        /// </summary>
        public Dictionary<string, string> ObfuscateOrigin()
        {
            if (!_stealthMode) EnableStealthMode();

            var headers = new Dictionary<string, string>();
            
            // Randomize user agent
            var agents = new[] 
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15",
                "Mozilla/5.0 (X11; Linux x86_64; rv:89.0) Gecko/20100101 Firefox/89.0"
            };
            headers["User-Agent"] = agents[new Random().Next(agents.Length)];
            
            // Spoof identifying headers
            headers["X-Forwarded-For"] = GenerateRandomIp();
            headers["X-Originating-IP"] = GenerateRandomIp();
            headers["Via"] = $"1.1 {GenerateRandomIp()}";
            
            EmitThought("◎ Origin obfuscated with randomized headers");
            return headers;
        }

        private string GenerateRandomIp()
        {
            var r = new Random();
            return $"{r.Next(1, 255)}.{r.Next(0, 255)}.{r.Next(0, 255)}.{r.Next(1, 255)}";
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
