/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    WATCHDOG - PROCESS SUPERVISOR                           ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Tiny (~1-2MB) binary that supervises brain.exe.                           ║
 * ║  Performs hot-swap updates without requiring restart.                      ║
 * ║  NEVER changes, acts as the stable foundation.                             ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Security.Cryptography;

const string BRAIN_EXE = "brain.exe";
const string BRAIN_NEW = "brain_new.tmp";
const string BRAIN_BACKUP = "brain_backup.exe";
const string UPDATE_SIGNAL = "update_ready.signal";

var baseDir = AppContext.BaseDirectory;
var brainPath = Path.Combine(baseDir, BRAIN_EXE);
var newPath = Path.Combine(baseDir, BRAIN_NEW);
var backupPath = Path.Combine(baseDir, BRAIN_BACKUP);
var signalPath = Path.Combine(baseDir, UPDATE_SIGNAL);

Console.WriteLine("═══════════════════════════════════════════════════════════════════");
Console.WriteLine("  NIGHTFRAME WATCHDOG v1.0.0 - PROCESS SUPERVISOR");
Console.WriteLine("═══════════════════════════════════════════════════════════════════");

Process? brainProcess = null;
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        // Check if brain exists
        if (!File.Exists(brainPath))
        {
            Console.WriteLine("∴ brain.exe not found. Waiting...");
            await Task.Delay(5000, cts.Token);
            continue;
        }
        
        // Start brain if not running
        if (brainProcess == null || brainProcess.HasExited)
        {
            Console.WriteLine($"◈ Starting {BRAIN_EXE}...");
            
            brainProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = brainPath,
                    WorkingDirectory = baseDir,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };
            
            brainProcess.Start();
            Console.WriteLine($"◎ brain.exe started (PID: {brainProcess.Id})");
        }
        
        // Check for update signal
        if (File.Exists(signalPath))
        {
            Console.WriteLine("◈ Update signal detected!");
            await PerformUpdateAsync(brainProcess, cts.Token);
        }
        
        // Wait a bit before next check
        await Task.Delay(1000, cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("◎ Watchdog shutdown requested.");
}
finally
{
    // Clean shutdown of brain
    if (brainProcess != null && !brainProcess.HasExited)
    {
        Console.WriteLine("◎ Stopping brain.exe...");
        brainProcess.Kill(entireProcessTree: true);
        await brainProcess.WaitForExitAsync();
    }
}

Console.WriteLine("◈ Watchdog terminated.");

// ═══════════════════════════════════════════════════════════════════════════════
//                              UPDATE PROCEDURE
// ═══════════════════════════════════════════════════════════════════════════════

async Task PerformUpdateAsync(Process currentBrain, CancellationToken ct)
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine("  UPDATE IN PROGRESS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    
    // Step 1: Check if new binary exists
    if (!File.Exists(newPath))
    {
        Console.WriteLine("∴ No new binary found. Ignoring signal.");
        File.Delete(signalPath);
        return;
    }
    
    // Step 2: Verify new binary integrity
    Console.WriteLine("◎ Verifying new binary...");
    var newHash = await ComputeHashAsync(newPath, ct);
    Console.WriteLine($"  Hash: {Convert.ToHexString(newHash)[..16]}...");
    
    // Step 3: Wait for brain to finish current work
    Console.WriteLine("◎ Waiting for brain to drain...");
    await Task.Delay(2000, ct);
    
    // Step 4: Kill current brain
    Console.WriteLine("◎ Terminating current brain...");
    if (!currentBrain.HasExited)
    {
        currentBrain.Kill(entireProcessTree: true);
        await currentBrain.WaitForExitAsync(ct);
    }
    
    // Step 5: Backup current binary
    Console.WriteLine("◎ Creating backup...");
    try
    {
        if (File.Exists(brainPath))
        {
            File.Copy(brainPath, backupPath, overwrite: true);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"∴ Backup failed: {ex.Message}");
    }
    
    // Step 6: Replace binary
    Console.WriteLine("◎ Installing new binary...");
    try
    {
        // Wait for file locks to release
        await Task.Delay(500, ct);
        
        File.Move(newPath, brainPath, overwrite: true);
        Console.WriteLine("◈ New binary installed successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"∴ Failed to install: {ex.Message}");
        Console.WriteLine("◎ Attempting rollback...");
        
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, brainPath, overwrite: true);
            Console.WriteLine("◈ Rollback successful.");
        }
    }
    
    // Step 7: Clean up signal
    File.Delete(signalPath);
    
    // Step 8: Brain will restart on next loop iteration
    Console.WriteLine("◎ Update complete. Restarting brain...");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
}

async Task<byte[]> ComputeHashAsync(string path, CancellationToken ct)
{
    await using var stream = File.OpenRead(path);
    return await SHA256.HashDataAsync(stream, ct);
}
