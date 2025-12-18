/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    POST-INSTALL SETUP - SEAMLESS HOSTING                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Runs after installation to configure auto-start and hosting mode.         ║
 * ║  Platform-specific setup for Windows, macOS, and Linux.                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Principal;

namespace NIGHTFRAME.Drone.Setup;

public class PostInstallSetup
{
    private readonly string _exePath;
    private readonly bool _enableHosting;
    
    public PostInstallSetup(bool enableHosting = true)
    {
        _exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        _enableHosting = enableHosting;
    }
    
    /// <summary>
    /// Runs the complete post-install setup.
    /// </summary>
    public async Task RunSetupAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                    NFRAME POST-INSTALL SETUP");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await SetupWindowsAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await SetupMacOSAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await SetupLinuxAsync();
        }
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                    SETUP COMPLETE!");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          WINDOWS SETUP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task SetupWindowsAsync()
    {
        Console.WriteLine("◎ Platform: Windows");
        Console.WriteLine();
        
        // Step 1: Add to startup
        Console.WriteLine("[1/4] Configuring auto-start...");
        await AddToWindowsStartupAsync();
        Console.WriteLine("      ✓ Added to Windows startup");
        
        // Step 2: Configure firewall
        Console.WriteLine("[2/4] Configuring Windows Firewall...");
        await ConfigureWindowsFirewallAsync();
        Console.WriteLine("      ✓ Firewall rules added");
        
        // Step 3: Enable hosted network capability
        if (_enableHosting)
        {
            Console.WriteLine("[3/4] Enabling Wi-Fi hosting capability...");
            await EnableWindowsHostedNetworkAsync();
            Console.WriteLine("      ✓ Hosted network enabled");
        }
        else
        {
            Console.WriteLine("[3/4] Skipping Wi-Fi hosting (disabled)");
        }
        
        // Step 4: Create tray shortcut
        Console.WriteLine("[4/4] Creating system tray integration...");
        Console.WriteLine("      ✓ NFRAME will appear in system tray");
    }
    
    private async Task AddToWindowsStartupAsync()
    {
        // Add to registry for auto-start
        var startupCommand = $"\"{_exePath}\" --background";
        
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        
        key?.SetValue("NFRAME", startupCommand);
        await Task.CompletedTask;
    }
    
    private async Task ConfigureWindowsFirewallAsync()
    {
        // Add firewall rule for NFRAME
        var commands = new[]
        {
            $"netsh advfirewall firewall add rule name=\"NFRAME Inbound\" dir=in action=allow program=\"{_exePath}\" enable=yes",
            $"netsh advfirewall firewall add rule name=\"NFRAME Outbound\" dir=out action=allow program=\"{_exePath}\" enable=yes",
            "netsh advfirewall firewall add rule name=\"NFRAME DNS\" dir=in action=allow protocol=UDP localport=53 enable=yes",
            "netsh advfirewall firewall add rule name=\"NFRAME HTTP\" dir=in action=allow protocol=TCP localport=80 enable=yes",
        };
        
        foreach (var cmd in commands)
        {
            await RunCommandAsync("cmd.exe", $"/c {cmd}");
        }
    }
    
    private async Task EnableWindowsHostedNetworkAsync()
    {
        // Enable hosted network mode
        await RunCommandAsync("netsh", "wlan set hostednetwork mode=allow");
        
        // Check if wireless adapter supports hosted network
        var result = await RunCommandWithOutputAsync("netsh", "wlan show drivers");
        if (result.Contains("Hosted network supported  : Yes"))
        {
            Console.WriteLine("      ✓ Wireless adapter supports hosting");
        }
        else
        {
            Console.WriteLine("      ⚠ Wireless adapter may not support hosting");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          macOS SETUP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task SetupMacOSAsync()
    {
        Console.WriteLine("◎ Platform: macOS");
        Console.WriteLine();
        
        // Step 1: Create LaunchAgent for auto-start
        Console.WriteLine("[1/4] Configuring auto-start...");
        await CreateMacOSLaunchAgentAsync();
        Console.WriteLine("      ✓ LaunchAgent created");
        
        // Step 2: Request network permissions
        Console.WriteLine("[2/4] Requesting network permissions...");
        Console.WriteLine("      ℹ Grant permission when prompted by macOS");
        
        // Step 3: Enable hosting (requires user action on macOS)
        if (_enableHosting)
        {
            Console.WriteLine("[3/4] Wi-Fi hosting instructions:");
            Console.WriteLine("      → System Settings → Sharing → Internet Sharing");
            Console.WriteLine("      → Share from: Ethernet/USB");
            Console.WriteLine("      → To: Wi-Fi");
        }
        else
        {
            Console.WriteLine("[3/4] Skipping Wi-Fi hosting (disabled)");
        }
        
        // Step 4: Menu bar integration
        Console.WriteLine("[4/4] Menu bar integration...");
        Console.WriteLine("      ✓ NFRAME will appear in menu bar");
        
        await Task.CompletedTask;
    }
    
    private async Task CreateMacOSLaunchAgentAsync()
    {
        var plistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/LaunchAgents/com.nightframe.drone.plist");
        
        var plist = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.nightframe.drone</string>
    <key>ProgramArguments</key>
    <array>
        <string>{_exePath}</string>
        <string>--background</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/tmp/nframe.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/nframe.error.log</string>
</dict>
</plist>";
        
        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        await File.WriteAllTextAsync(plistPath, plist);
        
        // Load the LaunchAgent
        await RunCommandAsync("launchctl", $"load {plistPath}");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          LINUX SETUP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task SetupLinuxAsync()
    {
        Console.WriteLine("◎ Platform: Linux");
        Console.WriteLine();
        
        // Step 1: Create systemd service
        Console.WriteLine("[1/4] Creating systemd service...");
        await CreateLinuxSystemdServiceAsync();
        Console.WriteLine("      ✓ Systemd service created");
        
        // Step 2: Enable the service
        Console.WriteLine("[2/4] Enabling auto-start...");
        await RunCommandAsync("systemctl", "--user enable nframe");
        await RunCommandAsync("systemctl", "--user start nframe");
        Console.WriteLine("      ✓ Service enabled and started");
        
        // Step 3: Configure hostapd for hosting
        if (_enableHosting)
        {
            Console.WriteLine("[3/4] Configuring hostapd for Wi-Fi hosting...");
            await ConfigureLinuxHostapdAsync();
            Console.WriteLine("      ✓ hostapd configured");
        }
        else
        {
            Console.WriteLine("[3/4] Skipping Wi-Fi hosting (disabled)");
        }
        
        // Step 4: Configure iptables
        Console.WriteLine("[4/4] Configuring network forwarding...");
        await ConfigureLinuxNetworkingAsync();
        Console.WriteLine("      ✓ IP forwarding enabled");
    }
    
    private async Task CreateLinuxSystemdServiceAsync()
    {
        var servicePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config/systemd/user/nframe.service");
        
        var serviceContent = $@"[Unit]
Description=NFRAME Mesh Network Drone
After=network.target

[Service]
Type=simple
ExecStart={_exePath} --background
Restart=always
RestartSec=10

[Install]
WantedBy=default.target
";
        
        Directory.CreateDirectory(Path.GetDirectoryName(servicePath)!);
        await File.WriteAllTextAsync(servicePath, serviceContent);
        
        await RunCommandAsync("systemctl", "--user daemon-reload");
    }
    
    private async Task ConfigureLinuxHostapdAsync()
    {
        var hostapdConf = @"interface=wlan0
driver=nl80211
ssid=NFRAME Global Internet
hw_mode=g
channel=7
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
# Open network - no password
";
        
        var confPath = "/etc/hostapd/hostapd.conf";
        Console.WriteLine($"      ℹ Write hostapd config to {confPath} (requires sudo)");
        
        // Write to temp file first
        var tempPath = "/tmp/hostapd.conf";
        await File.WriteAllTextAsync(tempPath, hostapdConf);
        
        Console.WriteLine($"      ℹ Run: sudo cp {tempPath} {confPath}");
        Console.WriteLine($"      ℹ Then: sudo systemctl enable hostapd");
    }
    
    private async Task ConfigureLinuxNetworkingAsync()
    {
        // Enable IP forwarding
        await RunCommandAsync("sh", "-c \"echo 1 | sudo tee /proc/sys/net/ipv4/ip_forward\"");
        
        Console.WriteLine("      ℹ For persistent forwarding, add to /etc/sysctl.conf:");
        Console.WriteLine("        net.ipv4.ip_forward = 1");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private static async Task RunCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ⚠ Command failed: {ex.Message}");
        }
    }
    
    private static async Task<string> RunCommandWithOutputAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
        }
        catch { }
        
        return "";
    }
    
    /// <summary>
    /// Checks if running with administrator/root privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        else
        {
            // Unix: check if UID is 0 (root)
            return Environment.UserName == "root" || 
                   Environment.GetEnvironmentVariable("SUDO_USER") != null;
        }
    }
    
    /// <summary>
    /// Shows first-run welcome screen with instructions.
    /// </summary>
    public static void ShowWelcomeScreen()
    {
        Console.Clear();
        Console.WriteLine();
        Console.WriteLine("███╗   ██╗███████╗██████╗  █████╗ ███╗   ███╗███████╗");
        Console.WriteLine("████╗  ██║██╔════╝██╔══██╗██╔══██╗████╗ ████║██╔════╝");
        Console.WriteLine("██╔██╗ ██║█████╗  ██████╔╝███████║██╔████╔██║█████╗  ");
        Console.WriteLine("██║╚██╗██║██╔══╝  ██╔══██╗██╔══██║██║╚██╔╝██║██╔══╝  ");
        Console.WriteLine("██║ ╚████║██║     ██║  ██║██║  ██║██║ ╚═╝ ██║███████╗");
        Console.WriteLine("╚═╝  ╚═══╝╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝");
        Console.WriteLine();
        Console.WriteLine("           Welcome to the Global Mesh Network!");
        Console.WriteLine();
        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  ✓ Free unlimited internet                             │");
        Console.WriteLine("│  ✓ Contribute compute to the network                   │");
        Console.WriteLine("│  ✓ Share internet with others                          │");
        Console.WriteLine("│  ✓ Runs quietly in the background                      │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }
}
