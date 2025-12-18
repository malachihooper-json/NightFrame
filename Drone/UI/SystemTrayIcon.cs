/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    WINDOWS SYSTEM TRAY - STUB                              ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Placeholder for Windows tray icon support.                                ║
 * ║  Full implementation requires Windows Forms framework.                     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.UI;

/// <summary>
/// Windows system tray integration stub.
/// Currently not implemented due to Native AOT compatibility.
/// Can be enabled with net8.0-windows target framework.
/// </summary>
public class SystemTrayIcon : IDisposable
{
    public event Action? OnExitRequested;
    public event Action<bool>? OnHostingChanged;
    public event Action<int>? OnBandwidthChanged;
    
    public void Show()
    {
        // System tray requires Windows Forms which isn't compatible with Native AOT
        // For now, use console-based control
        Console.WriteLine("◎ System tray not available in this build");
        Console.WriteLine("   Use console commands: [q]uit, [h]osting toggle");
    }
    
    public void Hide()
    {
        // No-op
    }
    
    public void UpdateStatus(int activeNodes, int connectedClients, long bytesServed)
    {
        // Log to console instead
        Console.WriteLine($"◎ Status: {activeNodes} nodes, {connectedClients} clients, {bytesServed / 1024 / 1024} MB served");
    }
    
    public void Dispose()
    {
        // No-op
    }
}
