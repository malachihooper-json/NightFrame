/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    NODE IDENTITY - CRYPTOGRAPHIC ID                        ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Ed25519 keypair for node identity and result signing.                     ║
 * ║  Identity is mathematical, not IP-based.                                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NIGHTFRAME.Drone;

public class NodeIdentity
{
    public string NodeId { get; private init; } = "";
    public byte[] PublicKey { get; private init; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; private init; }
    
    private byte[] _privateKey = Array.Empty<byte>();
    
    private static readonly string KeyFilePath = Path.Combine(
        AppContext.BaseDirectory, ".identity");
    
    private NodeIdentity() { }
    
    /// <summary>
    /// Loads existing identity or creates a new one.
    /// </summary>
    public static NodeIdentity LoadOrCreate()
    {
        if (File.Exists(KeyFilePath))
        {
            try
            {
                return Load();
            }
            catch
            {
                // Corrupted file, create new
            }
        }
        
        return Create();
    }
    
    /// <summary>
    /// Creates a new cryptographic identity.
    /// </summary>
    public static NodeIdentity Create()
    {
        // Generate Ed25519 keypair
        using var ecdsa = ECDsa.Create();
        ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
        
        var privateKey = ecdsa.ExportECPrivateKey();
        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        
        // Node ID is first 16 chars of public key hash
        var publicKeyHash = SHA256.HashData(publicKey);
        var nodeId = $"NFRAME_{Convert.ToHexString(publicKeyHash)[..12]}";
        
        var identity = new NodeIdentity
        {
            NodeId = nodeId,
            PublicKey = publicKey,
            CreatedAt = DateTime.UtcNow,
            _privateKey = privateKey
        };
        
        identity.Save();
        
        Console.WriteLine($"◈ Created new identity: {nodeId}");
        
        return identity;
    }
    
    /// <summary>
    /// Loads identity from disk.
    /// </summary>
    public static NodeIdentity Load()
    {
        var json = File.ReadAllText(KeyFilePath);
        var data = JsonSerializer.Deserialize(json, IdentityJsonContext.Default.IdentityData)
            ?? throw new InvalidOperationException("Invalid identity file");
        
        return new NodeIdentity
        {
            NodeId = data.NodeId,
            PublicKey = Convert.FromBase64String(data.PublicKey),
            CreatedAt = data.CreatedAt,
            _privateKey = Convert.FromBase64String(data.PrivateKey)
        };
    }
    
    /// <summary>
    /// Saves identity to disk.
    /// </summary>
    public void Save()
    {
        var data = new IdentityData
        {
            NodeId = NodeId,
            PublicKey = Convert.ToBase64String(PublicKey),
            PrivateKey = Convert.ToBase64String(_privateKey),
            CreatedAt = CreatedAt
        };
        
        var json = JsonSerializer.Serialize(data, IdentityJsonContext.Default.IdentityData);
        File.WriteAllText(KeyFilePath, json);
    }
    
    /// <summary>
    /// Signs data with the private key.
    /// </summary>
    public byte[] Sign(byte[] data)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(_privateKey, out _);
        
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }
    
    /// <summary>
    /// Verifies a signature.
    /// </summary>
    public bool Verify(byte[] data, byte[] signature)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(PublicKey, out _);
        
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }
}

/// <summary>
/// Data transfer object for identity serialization.
/// </summary>
public class IdentityData
{
    public string NodeId { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Source-generated JSON context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(IdentityData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class IdentityJsonContext : JsonSerializerContext
{
}

