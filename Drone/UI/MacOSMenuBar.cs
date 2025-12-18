/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    macOS MENU BAR INTEGRATION                              ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Shows NFRAME icon in macOS menu bar for easy access.                      ║
 * ║  Uses native macOS NSStatusBar via P/Invoke or Console for now.            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.UI;

/// <summary>
/// macOS menu bar integration.
/// Note: Full native integration requires AppKit bindings.
/// This implementation provides console-based status updates.
/// </summary>
public class MacOSMenuBar : IDisposable
{
    private bool _isHosting = true;
    private Timer? _statusTimer;
    
    public event Action? OnExitRequested;
    public event Action<bool>? OnHostingChanged;
    
    public void Show()
    {
        if (!OperatingSystem.IsMacOS()) return;
        
        Console.WriteLine("◈ NFRAME running in menu bar mode");
        Console.WriteLine("  Commands: [h]osting toggle, [s]tats, [q]uit");
        
        // Start status update timer
        _statusTimer = new Timer(_ => PrintStatus(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        
        // Start command listener in background
        _ = Task.Run(ListenForCommands);
    }
    
    private void PrintStatus()
    {
        // Print periodic status to console
        Console.WriteLine($"◎ [{DateTime.Now:HH:mm}] NFRAME active | Hosting: {(_isHosting ? "ON" : "OFF")}");
    }
    
    private async Task ListenForCommands()
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                
                switch (key.KeyChar)
                {
                    case 'h':
                    case 'H':
                        _isHosting = !_isHosting;
                        Console.WriteLine($"◎ Hosting: {(_isHosting ? "ENABLED" : "DISABLED")}");
                        OnHostingChanged?.Invoke(_isHosting);
                        break;
                    
                    case 's':
                    case 'S':
                        PrintStats();
                        break;
                    
                    case 'q':
                    case 'Q':
                        Console.WriteLine("◎ Exit requested...");
                        OnExitRequested?.Invoke();
                        return;
                }
            }
            
            await Task.Delay(100);
        }
    }
    
    private void PrintStats()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  NFRAME Statistics");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Hosting: {(_isHosting ? "Enabled" : "Disabled")}");
        Console.WriteLine($"  Uptime: {(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime):hh\\:mm\\:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }
    
    public void UpdateStatus(int activeNodes, int connectedClients)
    {
        // Could trigger a terminal-notifier notification on macOS
        // For now, just log
        Console.WriteLine($"◎ Status: {activeNodes} nodes, {connectedClients} clients");
    }
    
    public void Dispose()
    {
        _statusTimer?.Dispose();
    }
}
