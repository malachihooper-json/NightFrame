/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    CONSENT FLOW                                            ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Multi-step consent acknowledgment for captive portal operators.           ║
 * ║  Required before enabling internet sharing.                                ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Setup;

/// <summary>
/// Manages the 5-point consent flow for captive portal operators.
/// </summary>
public class ConsentFlow
{
    private readonly Network.SharingConfigManager _configManager;
    
    /// <summary>
    /// The 5 consent points that must be acknowledged.
    /// </summary>
    public static readonly ConsentPoint[] ConsentPoints = new[]
    {
        new ConsentPoint
        {
            Id = 1,
            Title = "Bandwidth Consumption",
            Description = "I understand that guest traffic will consume my internet bandwidth. " +
                "Every byte downloaded by guests counts against my internet connection.",
            PropertyName = nameof(Network.ConsentAcknowledgments.BandwidthConsumption)
        },
        new ConsentPoint
        {
            Id = 2,
            Title = "Billing Impact",
            Description = "I understand that if I have a data cap or metered billing, " +
                "guest usage may affect my internet bill. I will configure appropriate limits.",
            PropertyName = nameof(Network.ConsentAcknowledgments.BillingImpact)
        },
        new ConsentPoint
        {
            Id = 3,
            Title = "ISP Terms Compliance",
            Description = "I confirm that my internet service agreement permits hotspot or sharing usage, " +
                "OR I accept any risk of ISP terms violation.",
            PropertyName = nameof(Network.ConsentAcknowledgments.IspTermsCompliance)
        },
        new ConsentPoint
        {
            Id = 4,
            Title = "IP Address Liability",
            Description = "I understand that guest traffic will appear to originate from my IP address. " +
                "Session logs will be maintained to help demonstrate other devices were connected.",
            PropertyName = nameof(Network.ConsentAcknowledgments.IpLiabilityAwareness)
        },
        new ConsentPoint
        {
            Id = 5,
            Title = "Limit Configuration",
            Description = "I accept responsibility for configuring appropriate bandwidth limits " +
                "to protect myself from unexpected costs or service disruption.",
            PropertyName = nameof(Network.ConsentAcknowledgments.LimitConfiguration)
        }
    };
    
    public ConsentFlow(Network.SharingConfigManager configManager)
    {
        _configManager = configManager;
    }
    
    /// <summary>
    /// Runs the interactive consent flow in the console.
    /// </summary>
    public async Task<bool> RunInteractiveConsent()
    {
        Console.Clear();
        PrintHeader();
        
        Console.WriteLine();
        Console.WriteLine("Before enabling internet sharing through the captive portal,");
        Console.WriteLine("you must acknowledge each of the following 5 points.");
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
        
        var acknowledgments = new Network.ConsentAcknowledgments();
        
        foreach (var point in ConsentPoints)
        {
            Console.Clear();
            PrintHeader();
            PrintProgress(point.Id);
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"POINT {point.Id}: {point.Title.ToUpper()}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(WrapText(point.Description, 70));
            Console.WriteLine();
            
            Console.Write("Do you acknowledge and accept? [Y/N]: ");
            
            while (true)
            {
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.Y)
                {
                    Console.WriteLine("Y");
                    SetAcknowledgment(acknowledgments, point.PropertyName, true);
                    await Task.Delay(300); // Brief pause for feedback
                    break;
                }
                else if (key.Key == ConsoleKey.N)
                {
                    Console.WriteLine("N");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must acknowledge all points to enable sharing.");
                    Console.WriteLine("Sharing will remain disabled.");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                    return false;
                }
            }
        }
        
        // All points acknowledged - configure limits
        Console.Clear();
        PrintHeader();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ All consent points acknowledged!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Now let's configure your sharing limits.");
        Console.WriteLine();
        
        // Configure bandwidth limit
        var config = _configManager.GetConfig();
        
        Console.Write("Monthly bandwidth limit for guests (GB) [default: 50]: ");
        var limitInput = Console.ReadLine();
        if (double.TryParse(limitInput, out var limit) && limit >= 0)
        {
            config.MonthlyBandwidthLimitGB = limit;
        }
        else
        {
            config.MonthlyBandwidthLimitGB = 50;
        }
        
        // Ask about data cap
        Console.Write("Does your internet plan have a data cap? [Y/N]: ");
        var capKey = Console.ReadKey(true);
        config.HasDataCap = capKey.Key == ConsoleKey.Y;
        Console.WriteLine(config.HasDataCap ? "Y" : "N");
        
        if (config.HasDataCap)
        {
            Console.Write("What is your total monthly cap (GB)? ");
            var capInput = Console.ReadLine();
            if (double.TryParse(capInput, out var userCap) && userCap > 0)
            {
                config.UserDataCapGB = userCap;
                
                // Suggest limit
                var suggestedLimit = Math.Min(config.MonthlyBandwidthLimitGB, userCap * 0.1);
                Console.WriteLine($"◎ Suggestion: Share no more than {suggestedLimit:F0} GB to stay under 10% of your cap.");
            }
        }
        
        // ISP info
        Console.Write("Who is your ISP? (optional, press Enter to skip): ");
        config.IspName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(config.IspName)) config.IspName = null;
        
        // Record consent
        _configManager.RecordConsent(acknowledgments);
        config.IsEnabled = true;
        _configManager.SaveConfig();
        
        // Summary
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              SHARING ENABLED SUCCESSFULLY                      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Monthly sharing limit: {config.MonthlyBandwidthLimitGB} GB");
        Console.WriteLine($"  Current usage: {config.CurrentMonthUsageGB:F2} GB");
        Console.WriteLine($"  Consent recorded at: {config.ConsentTimestamp}");
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
        
        return true;
    }
    
    /// <summary>
    /// Checks if consent has already been granted.
    /// </summary>
    public bool IsConsentGranted()
    {
        return _configManager.GetConfig().ConsentGranted;
    }
    
    /// <summary>
    /// Revokes consent and disables sharing.
    /// </summary>
    public void RevokeConsent()
    {
        var config = _configManager.GetConfig();
        config.ConsentGranted = false;
        config.IsEnabled = false;
        config.Acknowledgments = new Network.ConsentAcknowledgments();
        _configManager.SaveConfig();
        
        Console.WriteLine("◎ Consent revoked. Sharing disabled.");
    }
    
    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           NIGHTFRAME SHARING CONSENT                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
    
    private static void PrintProgress(int current)
    {
        Console.Write("Progress: ");
        for (int i = 1; i <= 5; i++)
        {
            if (i < current)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("● ");
            }
            else if (i == current)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("○ ");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("○ ");
            }
        }
        Console.ResetColor();
        Console.WriteLine($" ({current}/5)");
    }
    
    private static string WrapText(string text, int width)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";
        
        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(currentLine))
            {
                currentLine = word;
            }
            else if (currentLine.Length + 1 + word.Length <= width)
            {
                currentLine += " " + word;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }
        
        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);
        
        return string.Join(Environment.NewLine, lines);
    }
    
    private static void SetAcknowledgment(Network.ConsentAcknowledgments ack, string propertyName, bool value)
    {
        var prop = typeof(Network.ConsentAcknowledgments).GetProperty(propertyName);
        prop?.SetValue(ack, value);
    }
}

/// <summary>
/// Represents a single consent point.
/// </summary>
public class ConsentPoint
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string PropertyName { get; init; }
}
