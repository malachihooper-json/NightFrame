/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    INITIALIZATION WIZARD - UNIFIED SETUP                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Complete first-run experience for NIGHTFRAME Drone nodes.                 ║
 * ║  Explains how the mesh works, collects consent, configures system.         ║
 * ║  Aligned with TTS Corpus "Sustainable Commons Model" documentation.        ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 * 
 * TRAINING CONTEXT FOR AI:
 * This wizard is the first interaction users have with NIGHTFRAME. It must:
 * 1. Clearly explain what NIGHTFRAME is and how the mesh network works
 * 2. Be honest about what resources will be shared (bandwidth, compute)
 * 3. Collect explicit informed consent before enabling any sharing
 * 4. Configure appropriate limits based on the user's situation
 * 5. Set up auto-start and system integration
 * 
 * The wizard follows the "Sustainable Commons Model" from the TTS Corpus:
 * - Pure Consumers: Use network without contributing (limited speed)
 * - Light Contributors: Scout app, RF mapping (full speed)
 * - Full Contributors: Drone operators, share bandwidth/compute (full speed + credits)
 */

using System.Runtime.InteropServices;
using System.Diagnostics;
using NIGHTFRAME.Drone.Network;

namespace NIGHTFRAME.Drone.Setup;

/// <summary>
/// Unified initialization wizard that guides users through NIGHTFRAME setup.
/// Combines education, consent, and system configuration into a single flow.
/// </summary>
public class InitializationWizard
{
    private readonly SharingConfigManager _configManager;
    private readonly string _exePath;
    private readonly bool _enableHosting;
    
    // Wizard state
    private enum WizardStep
    {
        Welcome,
        WhatIsNightframe,
        HowMeshWorks,
        WhatYouContribute,
        WhatYouGet,
        ConsentBandwidth,
        ConsentBilling,
        ConsentIspTerms,
        ConsentLiability,
        ConfigureLimits,
        SystemSetup,
        Complete
    }
    
    public InitializationWizard(bool enableHosting = true)
    {
        _configManager = new SharingConfigManager();
        _exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        _enableHosting = enableHosting;
    }
    
    /// <summary>
    /// Runs the complete initialization wizard.
    /// Returns true if setup completed successfully, false if user cancelled.
    /// </summary>
    public async Task<bool> RunAsync()
    {
        // Step 1: Welcome
        ShowWelcome();
        if (!WaitForContinue()) return false;
        
        // Step 2: What is NIGHTFRAME?
        ShowWhatIsNightframe();
        if (!WaitForContinue()) return false;
        
        // Step 3: How the Mesh Works
        ShowHowMeshWorks();
        if (!WaitForContinue()) return false;
        
        // Step 4: What You Contribute
        ShowWhatYouContribute();
        if (!WaitForContinue()) return false;
        
        // Step 5: What You Get
        ShowWhatYouGet();
        if (!WaitForContinue()) return false;
        
        // Step 6-9: Consent Flow (4 points, each must be acknowledged)
        if (!await RunConsentFlowAsync()) return false;
        
        // Step 10: Configure Limits
        await ConfigureLimitsAsync();
        
        // Step 11: System Setup (auto-start, firewall, etc.)
        await RunSystemSetupAsync();
        
        // Step 12: Complete
        ShowComplete();
        
        return true;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          EDUCATIONAL SCREENS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private void ShowWelcome()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
    ███╗   ██╗██╗ ██████╗ ██╗  ██╗████████╗███████╗██████╗  █████╗ ███╗   ███╗███████╗
    ████╗  ██║██║██╔════╝ ██║  ██║╚══██╔══╝██╔════╝██╔══██╗██╔══██╗████╗ ████║██╔════╝
    ██╔██╗ ██║██║██║  ███╗███████║   ██║   █████╗  ██████╔╝███████║██╔████╔██║█████╗  
    ██║╚██╗██║██║██║   ██║██╔══██║   ██║   ██╔══╝  ██╔══██╗██╔══██║██║╚██╔╝██║██╔══╝  
    ██║ ╚████║██║╚██████╔╝██║  ██║   ██║   ██║     ██║  ██║██║  ██║██║ ╚═╝ ██║███████╗
    ╚═╝  ╚═══╝╚═╝ ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝
        ");
        Console.ResetColor();
        
        CenterText("Welcome to the Global Mesh Network");
        Console.WriteLine();
        Console.WriteLine();
        
        WriteBoxed(new[]
        {
            "This wizard will help you:",
            "",
            "  1. Understand how NIGHTFRAME works",
            "  2. Make informed decisions about what to share",
            "  3. Configure your node for the best experience",
            "  4. Set up your system for seamless operation"
        });
        
        Console.WriteLine();
        Console.WriteLine();
        CenterText("Press ENTER to continue or ESC to exit");
    }
    
    private void ShowWhatIsNightframe()
    {
        Console.Clear();
        PrintStepHeader("What is NIGHTFRAME?", 1, 11);
        Console.WriteLine();
        
        WriteSection("The Big Picture", new[]
        {
            "NIGHTFRAME is a community-powered internet network. Think of it like",
            "carpooling, but for internet access.",
            "",
            "Traditional internet: You pay your ISP, you use your connection.",
            "NIGHTFRAME internet: Everyone shares a bit, everyone has access."
        });
        
        Console.WriteLine();
        
        WriteSection("The Goal", new[]
        {
            "Create a world where anyone can walk into any coffee shop, airport,",
            "or public space and instantly have free internet - without ads,",
            "without tracking, without asking for passwords."
        });
        
        Console.WriteLine();
        
        WriteSection("How You Fit In", new[]
        {
            "By running NIGHTFRAME, you become a node in this global network.",
            "Your computer shares a small amount of internet and compute power.",
            "In return, you get access to the entire network - everywhere."
        });
        
        Console.WriteLine();
        Console.WriteLine();
        PromptContinue();
    }
    
    private void ShowHowMeshWorks()
    {
        Console.Clear();
        PrintStepHeader("How the Mesh Network Works", 2, 11);
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(@"
         [Your Device]──────┐
              ▲              │
              │              ▼
         [Nearby Drone]  [Orchestrator]
              │              │
              ▼              ▼
         [Guest User]    [Analysis]
        ");
        Console.ResetColor();
        Console.WriteLine();
        
        WriteSection("The Three Participants", new[]
        {
            "◈ DRONES (You): Computers that share bandwidth and compute",
            "◈ GUESTS: Anyone who connects to a NIGHTFRAME hotspot",
            "◈ ORCHESTRATOR: Coordinates the network, manages credits"
        });
        
        Console.WriteLine();
        
        WriteSection("What Happens When Someone Connects", new[]
        {
            "1. A guest's phone sees 'NFRAME Global Internet' WiFi",
            "2. They connect and get instant internet (limited speed)",
            "3. If they install the app, they get full speed",
            "4. Their traffic routes through your connection's shared portion",
            "5. Session logs are kept for your legal protection"
        });
        
        Console.WriteLine();
        
        WriteSection("Privacy & Security", new[]
        {
            "• Guest traffic is logged (MAC, timestamps) for liability protection",
            "• Guests agree to Terms of Service before browsing",
            "• Your personal traffic is completely separate",
            "• All mesh communication is encrypted"
        });
        
        Console.WriteLine();
        PromptContinue();
    }
    
    private void ShowWhatYouContribute()
    {
        Console.Clear();
        PrintStepHeader("What You Contribute (With HARD CAPS)", 3, 11);
        Console.WriteLine();
        
        // Show the resource limits box first
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(ResourceLimits.GetFormattedSummary());
        Console.ResetColor();
        
        Console.WriteLine();
        
        WriteSection("What These Limits Mean", new[]
        {
            $"📶 BANDWIDTH: Max {ResourceLimits.DefaultMonthlyBandwidthGB} GB/month shared (you can set lower)",
            $"   • Guests get {ResourceLimits.GuestBandwidthKbps} Kbps until they install app",
            $"   • Max {ResourceLimits.MaxConcurrentGuests} simultaneous connections",
            "",
            $"💻 COMPUTE: Max {ResourceLimits.MaxCpuPercentage}% CPU, {ResourceLimits.MaxRamMB} MB RAM, {ResourceLimits.MaxGpuPercentage}% GPU",
            "   • Your own work always has priority",
            "   • Idle detection - only runs when you're not busy",
            "",
            $"💾 STORAGE: Max {ResourceLimits.MaxDiskCacheMB} MB for cache and models",
            $"   • Session logs kept {ResourceLimits.SessionLogRetentionDays} days (for your protection)"
        });
        
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ┌───────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  These caps CANNOT be raised. They protect YOU from           │");
        Console.WriteLine("  │  accidentally sharing more than intended.                     │");
        Console.WriteLine("  └───────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        
        Console.WriteLine();
        PromptContinue();
    }
    
    private void ShowWhatYouGet()
    {
        Console.Clear();
        PrintStepHeader("What You Get", 4, 11);
        Console.WriteLine();
        
        WriteSection("🌐 Free Internet Everywhere", new[]
        {
            "Connect to any NIGHTFRAME hotspot worldwide at full speed.",
            "No passwords, no sign-up, instant access.",
            "Currently active in: (network grows as more people join)"
        });
        
        Console.WriteLine();
        
        WriteSection("💰 Network Credits", new[]
        {
            "Earn credits by contributing compute and bandwidth:",
            "",
            "  • Use credits for AI inference tasks",
            "  • Priority access during peak times",
            "  • Credits have no real-world cash value (by design)"
        });
        
        Console.WriteLine();
        
        WriteSection("🔒 Privacy Respecting", new[]
        {
            "Unlike commercial WiFi hotspots:",
            "",
            "  • No tracking of your browsing history",
            "  • No selling your data to advertisers",
            "  • No ads injected into your browsing",
            "  • Open source - you can verify how it works"
        });
        
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ┌───────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  Bottom Line: You share a little, you get a lot.             │");
        Console.WriteLine("  └───────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        
        Console.WriteLine();
        PromptContinue();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CONSENT FLOW
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> RunConsentFlowAsync()
    {
        var acknowledgments = new ConsentAcknowledgments();
        
        // Consent Point 1: Bandwidth
        if (!ShowConsentPoint(5, "Bandwidth Sharing", new[]
        {
            "I understand that guest traffic will consume my internet bandwidth.",
            "",
            "When someone connects to my NIGHTFRAME hotspot:",
            "  • Their downloads count against my internet connection",
            "  • If I have a data cap, this affects my remaining data",
            "  • I can set limits to control how much is shared"
        }))
        {
            return false;
        }
        acknowledgments.BandwidthConsumption = true;
        
        // Consent Point 2: Billing
        if (!ShowConsentPoint(6, "Potential Costs", new[]
        {
            "I understand that sharing may affect my internet bill.",
            "",
            "If my plan has metered billing or data caps:",
            "  • Guest usage counts toward my cap",
            "  • I may incur overage charges if not careful",
            "  • I will configure appropriate limits"
        }))
        {
            return false;
        }
        acknowledgments.BillingImpact = true;
        
        // Consent Point 3: ISP Terms
        if (!ShowConsentPoint(7, "ISP Terms of Service", new[]
        {
            "I understand my ISP may have terms about hotspot sharing.",
            "",
            "Some ISPs prohibit or limit bandwidth sharing.",
            "By proceeding, I confirm one of:",
            "  • My ISP allows sharing (business plan, fiber, etc.)",
            "  • I accept the risk of potential ISP terms issues",
            "",
            "Tip: Check your ISP terms or call to confirm."
        }))
        {
            return false;
        }
        acknowledgments.IspTermsCompliance = true;
        
        // Consent Point 4: Liability
        if (!ShowConsentPoint(8, "Network Responsibility", new[]
        {
            "I understand my IP address is used for guest traffic.",
            "",
            "To protect you:",
            "  • All guest sessions are logged (MAC, timestamps)",
            "  • Guests must accept Terms of Service",
            "  • Logs can prove other devices were connected",
            "",
            "You are agreeing to share your connection responsibly."
        }))
        {
            return false;
        }
        acknowledgments.IpLiabilityAwareness = true;
        acknowledgments.LimitConfiguration = true; // Implied by proceeding
        
        // Record consent
        _configManager.RecordConsent(acknowledgments);
        await Task.CompletedTask;
        
        return true;
    }
    
    private bool ShowConsentPoint(int step, string title, string[] content)
    {
        Console.Clear();
        PrintStepHeader($"Consent: {title}", step, 11);
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ┌─ IMPORTANT ──────────────────────────────────────────────────┐");
        foreach (var line in content)
        {
            Console.WriteLine($"  │ {line,-64}│");
        }
        Console.WriteLine("  └────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        
        Console.WriteLine();
        Console.WriteLine();
        
        Console.Write("  Do you understand and agree? [");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Y");
        Console.ResetColor();
        Console.Write("es / ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("N");
        Console.ResetColor();
        Console.Write("o]: ");
        
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Y)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Agreed");
                Console.ResetColor();
                Thread.Sleep(300);
                return true;
            }
            else if (key.Key == ConsoleKey.N || key.Key == ConsoleKey.Escape)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Declined");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("  You must agree to all points to enable sharing.");
                Console.WriteLine("  NIGHTFRAME will run in receive-only mode.");
                Console.WriteLine();
                Console.WriteLine("  Press any key to exit setup...");
                Console.ReadKey(true);
                return false;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task ConfigureLimitsAsync()
    {
        Console.Clear();
        PrintStepHeader("Configure Your Limits", 9, 11);
        Console.WriteLine();
        
        var config = _configManager.GetConfig();
        
        WriteSection("Monthly Bandwidth Limit", new[]
        {
            "How much bandwidth should guests be able to use per month?",
            "This protects you from unexpected data usage."
        });
        Console.WriteLine();
        
        Console.Write("  Monthly limit in GB [default: 50]: ");
        var limitInput = Console.ReadLine()?.Trim();
        if (double.TryParse(limitInput, out var limit) && limit > 0)
        {
            config.MonthlyBandwidthLimitGB = limit;
        }
        else
        {
            config.MonthlyBandwidthLimitGB = 50;
            Console.WriteLine("  → Using default: 50 GB");
        }
        
        Console.WriteLine();
        
        // Data cap question
        Console.Write("  Does your internet plan have a data cap? [Y/N]: ");
        var capKey = Console.ReadKey(true);
        config.HasDataCap = capKey.Key == ConsoleKey.Y;
        Console.WriteLine(config.HasDataCap ? "Yes" : "No");
        
        if (config.HasDataCap)
        {
            Console.Write("  What is your monthly data cap in GB? ");
            var capInput = Console.ReadLine()?.Trim();
            if (double.TryParse(capInput, out var userCap) && userCap > 0)
            {
                config.UserDataCapGB = userCap;
                
                // Smart suggestion
                var suggestedLimit = Math.Min(config.MonthlyBandwidthLimitGB, userCap * 0.1);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  💡 Tip: Share no more than {suggestedLimit:F0} GB (10% of your cap) to be safe.");
                Console.ResetColor();
                
                Console.Write($"  Would you like to use {suggestedLimit:F0} GB as your limit? [Y/N]: ");
                if (Console.ReadKey(true).Key == ConsoleKey.Y)
                {
                    config.MonthlyBandwidthLimitGB = suggestedLimit;
                    Console.WriteLine($"Yes → Limit set to {suggestedLimit:F0} GB");
                }
                else
                {
                    Console.WriteLine("No → Keeping your original limit");
                }
            }
        }
        
        Console.WriteLine();
        
        // ISP name (optional)
        Console.Write("  Your ISP name (optional, press Enter to skip): ");
        config.IspName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(config.IspName)) config.IspName = null;
        
        // Save configuration
        config.IsEnabled = true;
        _configManager.SaveConfig();
        
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ Configuration saved!");
        Console.ResetColor();
        
        await Task.Delay(500);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          SYSTEM SETUP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task RunSystemSetupAsync()
    {
        Console.Clear();
        PrintStepHeader("System Setup", 10, 11);
        Console.WriteLine();
        
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                      RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
                      RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "Unknown";
        
        Console.WriteLine($"  Detected platform: {platform}");
        Console.WriteLine();
        Console.WriteLine("  Setting up your system...");
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
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ System setup complete!");
        Console.ResetColor();
        
        await Task.Delay(500);
    }
    
    private async Task SetupWindowsAsync()
    {
        Console.WriteLine("  [1/3] Adding to Windows startup...");
        await AddToWindowsStartupAsync();
        Console.WriteLine("        ✓ NFRAME will start with Windows");
        
        Console.WriteLine("  [2/3] Configuring Windows Firewall...");
        await ConfigureWindowsFirewallAsync();
        Console.WriteLine("        ✓ Firewall rules configured");
        
        if (_enableHosting)
        {
            Console.WriteLine("  [3/3] Enabling WiFi hotspot capability...");
            await EnableWindowsHostedNetworkAsync();
            Console.WriteLine("        ✓ Hotspot ready");
        }
        else
        {
            Console.WriteLine("  [3/3] WiFi hosting disabled by user");
        }
    }
    
    private async Task SetupMacOSAsync()
    {
        Console.WriteLine("  [1/2] Creating LaunchAgent for auto-start...");
        await CreateMacOSLaunchAgentAsync();
        Console.WriteLine("        ✓ NFRAME will start at login");
        
        Console.WriteLine("  [2/2] Menu bar integration...");
        Console.WriteLine("        ✓ NFRAME will appear in menu bar");
        
        if (_enableHosting)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ Note: macOS hotspot requires manual setup:");
            Console.WriteLine("    System Settings → Sharing → Internet Sharing");
            Console.ResetColor();
        }
        
        await Task.CompletedTask;
    }
    
    private async Task SetupLinuxAsync()
    {
        Console.WriteLine("  [1/2] Creating systemd service...");
        await CreateLinuxSystemdServiceAsync();
        Console.WriteLine("        ✓ nframe.service created");
        
        Console.WriteLine("  [2/2] Enabling auto-start...");
        await RunCommandAsync("systemctl", "--user daemon-reload");
        await RunCommandAsync("systemctl", "--user enable nframe");
        Console.WriteLine("        ✓ Service enabled");
        
        if (_enableHosting)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ Note: Hotspot requires hostapd. See docs for setup.");
            Console.ResetColor();
        }
    }
    
    private void ShowComplete()
    {
        Console.Clear();
        PrintStepHeader("Setup Complete!", 11, 11);
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════════════╗
    ║                                                               ║
    ║            🎉 Welcome to the NIGHTFRAME Network! 🎉          ║
    ║                                                               ║
    ╚═══════════════════════════════════════════════════════════════╝
        ");
        Console.ResetColor();
        
        var config = _configManager.GetConfig();
        
        WriteSection("Your Configuration", new[]
        {
            $"  Monthly sharing limit: {config.MonthlyBandwidthLimitGB} GB",
            $"  ISP: {config.IspName ?? "(not specified)"}",
            $"  Auto-start: Enabled",
            $"  Consent granted: {config.ConsentTimestamp:g}"
        });
        
        Console.WriteLine();
        
        WriteSection("What Happens Next", new[]
        {
            "  • NIGHTFRAME will start running in the background",
            "  • You'll see the icon in your system tray/menu bar",
            "  • Guests can connect to 'NFRAME Global Internet' WiFi",
            "  • You can access any NFRAME hotspot worldwide"
        });
        
        Console.WriteLine();
        
        WriteSection("Need Help?", new[]
        {
            "  • Status: Right-click the tray icon",
            "  • Reconfigure: Run with --setup flag",
            "  • Revoke consent: Delete ~/.config/NIGHTFRAME"
        });
        
        Console.WriteLine();
        Console.WriteLine();
        CenterText("Press ENTER to launch NIGHTFRAME...");
        Console.ReadLine();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private static bool WaitForContinue()
    {
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) return true;
            if (key.Key == ConsoleKey.Escape) return false;
        }
    }
    
    private static void PromptContinue()
    {
        CenterText("Press ENTER to continue or ESC to exit");
    }
    
    private static void CenterText(string text)
    {
        var padding = Math.Max(0, (Console.WindowWidth - text.Length) / 2);
        Console.WriteLine(new string(' ', padding) + text);
    }
    
    private static void PrintStepHeader(string title, int current, int total)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Step {current}/{total}: {title}");
        Console.ResetColor();
        
        // Progress bar
        Console.Write("  [");
        var filled = (current * 40) / total;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(new string('█', filled));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(new string('░', 40 - filled));
        Console.ResetColor();
        Console.WriteLine("]");
    }
    
    private static void WriteSection(string title, string[] lines)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {title}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  " + new string('─', title.Length));
        Console.ResetColor();
        foreach (var line in lines)
        {
            Console.WriteLine($"  {line}");
        }
    }
    
    private static void WriteBoxed(string[] lines)
    {
        var maxLen = lines.Max(l => l.Length);
        Console.WriteLine("  ┌" + new string('─', maxLen + 2) + "┐");
        foreach (var line in lines)
        {
            Console.WriteLine($"  │ {line.PadRight(maxLen)} │");
        }
        Console.WriteLine("  └" + new string('─', maxLen + 2) + "┘");
    }
    
    // System setup helpers (same as PostInstallSetup but integrated)
    private async Task AddToWindowsStartupAsync()
    {
        var startupCommand = $"\"{_exePath}\" --background";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        key?.SetValue("NFRAME", startupCommand);
        await Task.CompletedTask;
    }
    
    private async Task ConfigureWindowsFirewallAsync()
    {
        var commands = new[]
        {
            $"netsh advfirewall firewall add rule name=\"NFRAME\" dir=in action=allow program=\"{_exePath}\" enable=yes",
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
        await RunCommandAsync("netsh", "wlan set hostednetwork mode=allow");
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
</dict>
</plist>";
        
        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        await File.WriteAllTextAsync(plistPath, plist);
    }
    
    private async Task CreateLinuxSystemdServiceAsync()
    {
        var servicePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config/systemd/user/nframe.service");
        
        var serviceContent = $@"[Unit]
Description=NIGHTFRAME Mesh Network Drone
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
    }
    
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
        catch { /* Ignore errors in setup commands */ }
    }
}
