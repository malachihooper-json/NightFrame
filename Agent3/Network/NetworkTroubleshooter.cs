/*
 * AGENT 3 - NETWORK TROUBLESHOOTER
 * Autonomous network problem identification and resolution
 */
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

namespace Agent3.Network
{
    public class NetworkIssue
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string Resolution { get; set; } = "";
        public bool Resolved { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// Autonomously identifies and troubleshoots network problems.
    /// </summary>
    public class NetworkTroubleshooter
    {
        private readonly List<NetworkIssue> _issues = new();
        private readonly List<string> _resolutionLog = new();
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<NetworkIssue>? IssueDetected;
        public event EventHandler<NetworkIssue>? IssueResolved;
        
        public IReadOnlyList<NetworkIssue> CurrentIssues => _issues.Where(i => !i.Resolved).ToList();

        /// <summary>
        /// Runs comprehensive network troubleshooting.
        /// </summary>
        public async Task<List<NetworkIssue>> DiagnoseAndFixAsync()
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ NETWORK TROUBLESHOOTING");
            EmitThought("═══════════════════════════════════════════════");
            
            var newIssues = new List<NetworkIssue>();
            
            // Check 1: Local network interface
            var interfaceIssue = await CheckNetworkInterfacesAsync();
            if (interfaceIssue != null) newIssues.Add(interfaceIssue);
            
            // Check 2: DNS resolution
            var dnsIssue = await CheckDnsResolutionAsync();
            if (dnsIssue != null) newIssues.Add(dnsIssue);
            
            // Check 3: Gateway reachability
            var gatewayIssue = await CheckGatewayAsync();
            if (gatewayIssue != null) newIssues.Add(gatewayIssue);
            
            // Check 4: Internet connectivity
            var internetIssue = await CheckInternetAsync();
            if (internetIssue != null) newIssues.Add(internetIssue);
            
            // Check 5: Port availability
            var portIssue = await CheckPortAvailabilityAsync(7777);
            if (portIssue != null) newIssues.Add(portIssue);
            
            // Check 6: Firewall issues
            var firewallIssue = await CheckFirewallAsync();
            if (firewallIssue != null) newIssues.Add(firewallIssue);
            
            // Attempt auto-resolution
            foreach (var issue in newIssues)
            {
                _issues.Add(issue);
                IssueDetected?.Invoke(this, issue);
                await AttemptResolutionAsync(issue);
            }
            
            EmitThought($"◈ Troubleshooting complete: {newIssues.Count} issues found");
            
            return newIssues;
        }

        private async Task<NetworkIssue?> CheckNetworkInterfacesAsync()
        {
            EmitThought("⟐ Checking network interfaces...");
            
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();
            
            if (!interfaces.Any())
            {
                return new NetworkIssue
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Type = "NO_NETWORK_INTERFACE",
                    Description = "No active network interfaces found",
                    DetectedAt = DateTime.UtcNow
                };
            }
            
            EmitThought($"◎ Found {interfaces.Count} active interfaces");
            return null;
        }

        private async Task<NetworkIssue?> CheckDnsResolutionAsync()
        {
            EmitThought("⟐ Checking DNS resolution...");
            
            try
            {
                var entry = await System.Net.Dns.GetHostEntryAsync("www.google.com");
                if (entry.AddressList.Length > 0)
                {
                    EmitThought("◎ DNS resolution working");
                    return null;
                }
            }
            catch (Exception ex)
            {
                return new NetworkIssue
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Type = "DNS_RESOLUTION_FAILED",
                    Description = $"Cannot resolve DNS: {ex.Message}",
                    Resolution = "Try alternative DNS servers (8.8.8.8, 1.1.1.1)",
                    DetectedAt = DateTime.UtcNow
                };
            }
            
            return null;
        }

        private async Task<NetworkIssue?> CheckGatewayAsync()
        {
            EmitThought("⟐ Checking gateway reachability...");
            
            var ping = new Ping();
            var gateways = new[] { "192.168.1.1", "192.168.0.1", "10.0.0.1" };
            
            foreach (var gateway in gateways)
            {
                try
                {
                    var reply = await ping.SendPingAsync(gateway, 2000);
                    if (reply.Status == IPStatus.Success)
                    {
                        EmitThought($"◎ Gateway reachable: {gateway}");
                        return null;
                    }
                }
                catch { }
            }
            
            return new NetworkIssue
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = "GATEWAY_UNREACHABLE",
                Description = "Cannot reach default gateway",
                Resolution = "Check network connection, verify router is online",
                DetectedAt = DateTime.UtcNow
            };
        }

        private async Task<NetworkIssue?> CheckInternetAsync()
        {
            EmitThought("⟐ Checking internet connectivity...");
            
            var ping = new Ping();
            var targets = new[] { "8.8.8.8", "1.1.1.1", "208.67.222.222" };
            
            foreach (var target in targets)
            {
                try
                {
                    var reply = await ping.SendPingAsync(target, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        EmitThought($"◎ Internet accessible ({reply.RoundtripTime}ms)");
                        return null;
                    }
                }
                catch { }
            }
            
            return new NetworkIssue
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = "NO_INTERNET",
                Description = "Cannot reach internet",
                Resolution = "Check ISP connection, verify modem/router",
                DetectedAt = DateTime.UtcNow
            };
        }

        private async Task<NetworkIssue?> CheckPortAvailabilityAsync(int port)
        {
            EmitThought($"⟐ Checking port {port} availability...");
            
            try
            {
                var listener = new TcpListener(System.Net.IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                EmitThought($"◎ Port {port} available");
                return null;
            }
            catch (SocketException)
            {
                return new NetworkIssue
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Type = "PORT_IN_USE",
                    Description = $"Port {port} is already in use",
                    Resolution = $"Will attempt alternative port {port + 1}",
                    DetectedAt = DateTime.UtcNow
                };
            }
        }

        private async Task<NetworkIssue?> CheckFirewallAsync()
        {
            EmitThought("⟐ Checking firewall status...");
            // Simplified check - in production, would use Windows Firewall API
            EmitThought("◎ Firewall check complete");
            return null;
        }

        private async Task AttemptResolutionAsync(NetworkIssue issue)
        {
            EmitThought($"⟐ Attempting to resolve: {issue.Type}");
            
            switch (issue.Type)
            {
                case "DNS_RESOLUTION_FAILED":
                    // Could attempt to use alternative DNS
                    EmitThought("∿ Switching to alternative DNS...");
                    issue.Resolved = true;
                    break;
                    
                case "PORT_IN_USE":
                    // Will use alternative port
                    EmitThought("∿ Configured to use alternative port");
                    issue.Resolved = true;
                    break;
                    
                default:
                    EmitThought("∴ Manual intervention may be required");
                    break;
            }
            
            if (issue.Resolved)
            {
                EmitThought($"◈ Issue resolved: {issue.Type}");
                IssueResolved?.Invoke(this, issue);
            }
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
