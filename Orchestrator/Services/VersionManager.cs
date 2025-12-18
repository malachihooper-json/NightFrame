/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    VERSION MANAGER - UPDATE COORDINATOR                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Manages binary versions, canary deployments, and rollback logic.          ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Security.Cryptography;

namespace NIGHTFRAME.Orchestrator.Services;

public static class VersionManager
{
    public static string CurrentVersion { get; private set; } = "1.0.0";
    public static byte[] CurrentBinaryHash { get; private set; } = Array.Empty<byte>();
    public static string? CanaryVersion { get; private set; }
    public static DateTime? CanaryStartTime { get; private set; }
    
    private static readonly Dictionary<string, BinaryInfo> _binaries = new();
    private static readonly string _binaryStorePath;
    
    static VersionManager()
    {
        _binaryStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME", "binaries");
        
        Directory.CreateDirectory(_binaryStorePath);
        LoadBinaries();
    }
    
    /// <summary>
    /// Registers a new binary version for distribution.
    /// </summary>
    public static async Task RegisterBinaryAsync(string version, Stream binaryStream)
    {
        var filePath = Path.Combine(_binaryStorePath, $"brain_{version}.bin");
        
        // Calculate hash while saving
        using var sha256 = SHA256.Create();
        using var fileStream = File.Create(filePath);
        using var hashStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);
        
        await binaryStream.CopyToAsync(hashStream);
        hashStream.FlushFinalBlock();
        
        var hash = sha256.Hash!;
        
        _binaries[version] = new BinaryInfo
        {
            Version = version,
            FilePath = filePath,
            Hash = hash,
            Size = new FileInfo(filePath).Length,
            UploadedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Starts a canary deployment (5% of network).
    /// </summary>
    public static void StartCanaryDeployment(string version)
    {
        if (!_binaries.ContainsKey(version))
            throw new InvalidOperationException($"Binary version {version} not found");
        
        CanaryVersion = version;
        CanaryStartTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Promotes canary to full deployment after validation.
    /// </summary>
    public static void PromoteCanary()
    {
        if (CanaryVersion == null)
            throw new InvalidOperationException("No canary deployment active");
        
        CurrentVersion = CanaryVersion;
        CurrentBinaryHash = _binaries[CanaryVersion].Hash;
        
        CanaryVersion = null;
        CanaryStartTime = null;
    }
    
    /// <summary>
    /// Aborts canary deployment.
    /// </summary>
    public static void AbortCanary()
    {
        CanaryVersion = null;
        CanaryStartTime = null;
    }
    
    /// <summary>
    /// Checks if a node should receive the canary version.
    /// Uses deterministic selection based on node ID hash.
    /// </summary>
    public static bool ShouldReceiveCanary(string nodeId)
    {
        if (CanaryVersion == null) return false;
        
        // Deterministic 5% selection based on node ID
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(nodeId));
        var value = BitConverter.ToUInt32(hash, 0);
        return value % 100 < 5; // 5% of nodes
    }
    
    /// <summary>
    /// Gets the appropriate version for a node to download.
    /// </summary>
    public static (string Version, byte[] Hash) GetVersionForNode(string nodeId)
    {
        if (ShouldReceiveCanary(nodeId) && CanaryVersion != null)
        {
            return (CanaryVersion, _binaries[CanaryVersion].Hash);
        }
        
        return (CurrentVersion, CurrentBinaryHash);
    }
    
    /// <summary>
    /// Streams binary chunks for download.
    /// </summary>
    public static async IAsyncEnumerable<byte[]> StreamBinaryAsync(string version, int chunkSize = 64 * 1024)
    {
        if (!_binaries.TryGetValue(version, out var info))
            yield break;
        
        await using var stream = File.OpenRead(info.FilePath);
        var buffer = new byte[chunkSize];
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            if (bytesRead < chunkSize)
            {
                var partial = new byte[bytesRead];
                Array.Copy(buffer, partial, bytesRead);
                yield return partial;
            }
            else
            {
                yield return (byte[])buffer.Clone();
            }
        }
    }
    
    private static void LoadBinaries()
    {
        foreach (var file in Directory.GetFiles(_binaryStorePath, "brain_*.bin"))
        {
            var version = Path.GetFileNameWithoutExtension(file).Replace("brain_", "");
            var hash = SHA256.HashData(File.ReadAllBytes(file));
            
            _binaries[version] = new BinaryInfo
            {
                Version = version,
                FilePath = file,
                Hash = hash,
                Size = new FileInfo(file).Length,
                UploadedAt = File.GetCreationTimeUtc(file)
            };
        }
        
        // Set current version to latest
        if (_binaries.Any())
        {
            var latest = _binaries.OrderByDescending(b => b.Value.UploadedAt).First();
            CurrentVersion = latest.Key;
            CurrentBinaryHash = latest.Value.Hash;
        }
    }
    
    private class BinaryInfo
    {
        public required string Version { get; init; }
        public required string FilePath { get; init; }
        public required byte[] Hash { get; init; }
        public required long Size { get; init; }
        public required DateTime UploadedAt { get; init; }
    }
}
