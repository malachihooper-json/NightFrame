/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    HARDWARE AUDIT - CAPABILITY DISCOVERY                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Scans hardware to determine node capabilities for role assignment.        ║
 * ║  Provides FLOPS estimation for load balancing in distributed compute.     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Runtime.InteropServices;

namespace NIGHTFRAME.Drone.Hardware;

public class HardwareSpecs
{
    public long RamMb { get; init; }
    public int CpuCores { get; init; }
    public long DiskFreeMb { get; init; }
    public bool HasGpu { get; init; }
    public string GpuName { get; init; } = "";
    public long GpuVramMb { get; init; }
    public double CurrentCpuLoad { get; set; }
    public long EstimatedFlops { get; init; }        // NEW: Estimated compute capacity
    public string ExecutionProvider { get; init; } = "CPU"; // NEW: Best execution provider
    
    // Deprecated: Use BuildHardwareSpecs in DroneCore instead
    public NIGHTFRAME.Orchestrator.Grpc.HardwareSpecs ToProto() => new()
    {
        RamMb = RamMb,
        CpuCores = CpuCores,
        DiskFreeMb = DiskFreeMb,
        HasGpu = HasGpu,
        GpuName = GpuName,
        GpuVramMb = GpuVramMb,
        CurrentCpuLoad = CurrentCpuLoad,
        EstimatedFlops = EstimatedFlops,
        ExecutionProvider = ExecutionProvider
    };
}

public static class HardwareAudit
{
    public static HardwareSpecs Scan()
    {
        long ramMb = 0;
        int cpuCores = Environment.ProcessorCount;
        long diskFreeMb = 0;
        bool hasGpu = false;
        string gpuName = "";
        long gpuVramMb = 0;
        
        try
        {
            // Get RAM info
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ramMb = GetWindowsRamMb();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ramMb = GetLinuxRamMb();
            }
            else
            {
                ramMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            }
        }
        catch
        {
            ramMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        }
        
        try
        {
            // Get disk info
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();
            
            diskFreeMb = drives.Sum(d => d.AvailableFreeSpace) / (1024 * 1024);
        }
        catch
        {
            diskFreeMb = 0;
        }
        
        try
        {
            // Detect GPU
            (hasGpu, gpuName, gpuVramMb) = DetectGpu();
        }
        catch
        {
            hasGpu = false;
        }
        
        // Estimate FLOPS based on hardware
        var estimatedFlops = EstimateFlops(cpuCores, hasGpu, gpuName, gpuVramMb);
        var executionProvider = DetermineExecutionProvider(hasGpu, gpuName);
        
        return new HardwareSpecs
        {
            RamMb = ramMb,
            CpuCores = cpuCores,
            DiskFreeMb = diskFreeMb,
            HasGpu = hasGpu,
            GpuName = gpuName,
            GpuVramMb = gpuVramMb,
            CurrentCpuLoad = 0,
            EstimatedFlops = estimatedFlops,
            ExecutionProvider = executionProvider
        };
    }
    
    /// <summary>
    /// Estimates FLOPS capacity based on hardware.
    /// </summary>
    private static long EstimateFlops(int cpuCores, bool hasGpu, string gpuName, long gpuVramMb)
    {
        // Base CPU FLOPS estimate (conservative: 10 GFLOPS per core)
        long cpuFlops = cpuCores * 10_000_000_000L;
        
        if (!hasGpu)
            return cpuFlops;
        
        // GPU FLOPS estimates based on known architectures
        var gpuLower = gpuName.ToLowerInvariant();
        long gpuFlops = 0;
        
        // NVIDIA GPUs
        if (gpuLower.Contains("rtx 40"))
        {
            // RTX 4000 series - Ada Lovelace
            if (gpuLower.Contains("4090")) gpuFlops = 82_600_000_000_000L;      // 82.6 TFLOPS
            else if (gpuLower.Contains("4080")) gpuFlops = 48_700_000_000_000L; // 48.7 TFLOPS
            else if (gpuLower.Contains("4070")) gpuFlops = 29_200_000_000_000L; // 29.2 TFLOPS
            else gpuFlops = 20_000_000_000_000L; // Estimate for other 4000 series
        }
        else if (gpuLower.Contains("rtx 30"))
        {
            // RTX 3000 series - Ampere
            if (gpuLower.Contains("3090")) gpuFlops = 35_600_000_000_000L;      // 35.6 TFLOPS
            else if (gpuLower.Contains("3080")) gpuFlops = 29_800_000_000_000L; // 29.8 TFLOPS
            else if (gpuLower.Contains("3070")) gpuFlops = 20_300_000_000_000L; // 20.3 TFLOPS
            else if (gpuLower.Contains("3060")) gpuFlops = 12_700_000_000_000L; // 12.7 TFLOPS
            else gpuFlops = 15_000_000_000_000L;
        }
        else if (gpuLower.Contains("rtx 20"))
        {
            // RTX 2000 series - Turing
            if (gpuLower.Contains("2080")) gpuFlops = 14_200_000_000_000L;      // 14.2 TFLOPS
            else if (gpuLower.Contains("2070")) gpuFlops = 9_060_000_000_000L;  // 9.1 TFLOPS
            else if (gpuLower.Contains("2060")) gpuFlops = 6_450_000_000_000L;  // 6.5 TFLOPS
            else gpuFlops = 8_000_000_000_000L;
        }
        else if (gpuLower.Contains("gtx 1"))
        {
            // GTX 1000 series - Pascal
            if (gpuLower.Contains("1080")) gpuFlops = 8_900_000_000_000L;       // 8.9 TFLOPS
            else if (gpuLower.Contains("1070")) gpuFlops = 6_500_000_000_000L;  // 6.5 TFLOPS
            else if (gpuLower.Contains("1060")) gpuFlops = 4_400_000_000_000L;  // 4.4 TFLOPS
            else gpuFlops = 4_000_000_000_000L;
        }
        else if (gpuLower.Contains("tesla") || gpuLower.Contains("a100") || gpuLower.Contains("h100"))
        {
            // Data center GPUs
            if (gpuLower.Contains("h100")) gpuFlops = 1_979_000_000_000_000L;   // 1979 TFLOPS FP8
            else if (gpuLower.Contains("a100")) gpuFlops = 312_000_000_000_000L; // 312 TFLOPS FP16
            else gpuFlops = 50_000_000_000_000L;
        }
        else if (gpuLower.Contains("radeon") || gpuLower.Contains("rx"))
        {
            // AMD GPUs - rough estimates based on VRAM
            gpuFlops = gpuVramMb * 2_000_000L; // ~2 TFLOPS per GB as baseline
        }
        else
        {
            // Unknown GPU - estimate based on VRAM
            gpuFlops = gpuVramMb * 1_000_000L; // ~1 TFLOPS per GB as baseline
        }
        
        // Return GPU FLOPS (GPU dominates for neural compute)
        return gpuFlops > 0 ? gpuFlops : cpuFlops;
    }
    
    /// <summary>
    /// Determines the best ONNX execution provider based on hardware.
    /// </summary>
    private static string DetermineExecutionProvider(bool hasGpu, string gpuName)
    {
        if (!hasGpu)
            return "CPUExecutionProvider";
        
        var gpuLower = gpuName.ToLowerInvariant();
        
        // NVIDIA GPUs use CUDA
        if (gpuLower.Contains("geforce") || 
            gpuLower.Contains("rtx") || 
            gpuLower.Contains("gtx") ||
            gpuLower.Contains("tesla") ||
            gpuLower.Contains("quadro") ||
            gpuLower.Contains("nvidia"))
        {
            return "CUDAExecutionProvider";
        }
        
        // AMD GPUs can use DirectML on Windows
        if (gpuLower.Contains("radeon") || 
            gpuLower.Contains("rx") ||
            gpuLower.Contains("amd"))
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "DmlExecutionProvider" 
                : "CPUExecutionProvider";
        }
        
        // Intel GPUs (Arc)
        if (gpuLower.Contains("intel") || gpuLower.Contains("arc"))
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "DmlExecutionProvider" 
                : "CPUExecutionProvider";
        }
        
        // macOS uses CoreML
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "CoreMLExecutionProvider";
        }
        
        return "CPUExecutionProvider";
    }
    
    private static long GetWindowsRamMb()
    {
        // Use kernel32 GlobalMemoryStatusEx
        var memStatus = new MEMORYSTATUSEX();
        memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            return (long)(memStatus.ullTotalPhys / (1024 * 1024));
        }
        
        return 0;
    }
    
    private static long GetLinuxRamMb()
    {
        try
        {
            var memInfo = File.ReadAllLines("/proc/meminfo");
            var totalLine = memInfo.FirstOrDefault(l => l.StartsWith("MemTotal:"));
            if (totalLine != null)
            {
                var parts = totalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                {
                    return kb / 1024;
                }
            }
        }
        catch { }
        
        return 0;
    }
    
    private static (bool hasGpu, string name, long vramMb) DetectGpu()
    {
        // Try to detect NVIDIA GPU via nvidia-smi
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var parts = output.Trim().Split(',');
                    if (parts.Length >= 2)
                    {
                        var name = parts[0].Trim();
                        long.TryParse(parts[1].Trim(), out var vram);
                        return (true, name, vram);
                    }
                }
            }
        }
        catch { }
        
        // Try Windows WMI for other GPUs
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var result = DetectGpuViaWmi();
                if (result.hasGpu)
                    return result;
            }
            catch { }
        }
        
        return (false, "", 0);
    }
    
    private static (bool hasGpu, string name, long vramMb) DetectGpuViaWmi()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"Get-WmiObject Win32_VideoController | Select-Object Name, AdapterRAM | ConvertTo-Json\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // Simple parse - look for Name and AdapterRAM
                    if (output.Contains("Name"))
                    {
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(
                            output, "\"Name\"\\s*:\\s*\"([^\"]+)\"");
                        var ramMatch = System.Text.RegularExpressions.Regex.Match(
                            output, "\"AdapterRAM\"\\s*:\\s*(\\d+)");
                        
                        if (nameMatch.Success)
                        {
                            var name = nameMatch.Groups[1].Value;
                            long vramBytes = 0;
                            if (ramMatch.Success)
                            {
                                long.TryParse(ramMatch.Groups[1].Value, out vramBytes);
                            }
                            
                            // Check if it's a real GPU (not integrated)
                            var nameLower = name.ToLowerInvariant();
                            if (nameLower.Contains("nvidia") || 
                                nameLower.Contains("radeon") || 
                                nameLower.Contains("geforce") ||
                                nameLower.Contains("arc"))
                            {
                                return (true, name, vramBytes / (1024 * 1024));
                            }
                        }
                    }
                }
            }
        }
        catch { }
        
        return (false, "", 0);
    }
    
    // Windows memory status structure
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
