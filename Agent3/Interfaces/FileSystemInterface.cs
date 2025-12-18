/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - FILE SYSTEM INTERFACE                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Secure, controlled access to the host file system for           ║
 * ║           data handling, self-maintenance, and persistent storage         ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Agent3.Interfaces
{
    /// <summary>
    /// Represents a file operation result.
    /// </summary>
    public class FileOperationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Data { get; set; }
        public string? FilePath { get; set; }
        public long BytesProcessed { get; set; }
    }

    /// <summary>
    /// Access levels for file system operations.
    /// </summary>
    public enum FileAccessLevel
    {
        ReadOnly,       // Can only read files
        ReadWrite,      // Can read and write files
        Full            // Full access including execution
    }

    /// <summary>
    /// The File System Interface provides secure, sandboxed access to
    /// the host file system with comprehensive logging and safety checks.
    /// </summary>
    public class FileSystemInterface
    {
        private readonly string _baseDirectory;
        private readonly HashSet<string> _allowedExtensions;
        private readonly List<string> _operationLog;
        private readonly int _maxFileSizeBytes;
        private FileAccessLevel _accessLevel;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<string>? SecurityViolation;
        
        public FileSystemInterface(string baseDirectory, FileAccessLevel accessLevel = FileAccessLevel.ReadWrite)
        {
            _baseDirectory = Path.GetFullPath(baseDirectory);
            _accessLevel = accessLevel;
            _operationLog = new List<string>();
            _maxFileSizeBytes = 100 * 1024 * 1024; // 100 MB limit
            
            // Define allowed file extensions for safety
            _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".txt", ".json", ".xml", ".csv", ".log",
                ".md", ".yaml", ".yml", ".config",
                ".cs", ".py", ".js", ".ts", ".html", ".css"
            };
            
            // Ensure base directory exists
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
            
            EmitThought($"⟁ File System Interface initialized. Base: {_baseDirectory}");
        }
        
        /// <summary>
        /// Validates that a path is within the allowed sandbox.
        /// </summary>
        private bool ValidatePath(string path, out string fullPath)
        {
            try
            {
                fullPath = Path.GetFullPath(path);
                
                // Ensure path is within base directory (prevent directory traversal)
                if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    LogOperation($"SECURITY: Path outside sandbox: {path}");
                    SecurityViolation?.Invoke(this, $"Attempted access outside sandbox: {path}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                fullPath = string.Empty;
                LogOperation($"ERROR: Invalid path {path}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Reads a file within the sandbox.
        /// </summary>
        public async Task<FileOperationResult> ReadFileAsync(string relativePath)
        {
            var result = new FileOperationResult();
            
            if (!ValidatePath(Path.Combine(_baseDirectory, relativePath), out var fullPath))
            {
                result.Success = false;
                result.Error = "Path validation failed";
                return result;
            }
            
            result.FilePath = fullPath;
            
            if (!File.Exists(fullPath))
            {
                result.Success = false;
                result.Error = "File not found";
                return result;
            }
            
            try
            {
                EmitThought($"⟐ Reading file: {relativePath}");
                
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > _maxFileSizeBytes)
                {
                    result.Success = false;
                    result.Error = $"File exceeds size limit ({_maxFileSizeBytes} bytes)";
                    return result;
                }
                
                result.Data = await File.ReadAllTextAsync(fullPath);
                result.BytesProcessed = result.Data.Length;
                result.Success = true;
                
                LogOperation($"READ: {relativePath} ({result.BytesProcessed} bytes)");
                EmitThought($"◈ File read complete: {result.BytesProcessed} bytes");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                LogOperation($"ERROR: Read failed for {relativePath}: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Writes content to a file within the sandbox.
        /// </summary>
        public async Task<FileOperationResult> WriteFileAsync(string relativePath, string content, bool append = false)
        {
            var result = new FileOperationResult();
            
            if (_accessLevel == FileAccessLevel.ReadOnly)
            {
                result.Success = false;
                result.Error = "Write access not permitted";
                return result;
            }
            
            if (!ValidatePath(Path.Combine(_baseDirectory, relativePath), out var fullPath))
            {
                result.Success = false;
                result.Error = "Path validation failed";
                return result;
            }
            
            result.FilePath = fullPath;
            
            // Validate extension
            string extension = Path.GetExtension(fullPath);
            if (!_allowedExtensions.Contains(extension))
            {
                result.Success = false;
                result.Error = $"File extension not allowed: {extension}";
                LogOperation($"BLOCKED: Write to disallowed extension: {extension}");
                return result;
            }
            
            try
            {
                EmitThought($"⟐ Writing file: {relativePath}");
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                if (append)
                {
                    await File.AppendAllTextAsync(fullPath, content);
                }
                else
                {
                    await File.WriteAllTextAsync(fullPath, content);
                }
                
                result.BytesProcessed = content.Length;
                result.Success = true;
                
                LogOperation($"WRITE: {relativePath} ({result.BytesProcessed} bytes, append={append})");
                EmitThought($"◈ File write complete: {result.BytesProcessed} bytes");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                LogOperation($"ERROR: Write failed for {relativePath}: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Lists files and directories at a given path.
        /// </summary>
        public async Task<List<string>> ListDirectoryAsync(string relativePath = "")
        {
            var items = new List<string>();
            
            if (!ValidatePath(Path.Combine(_baseDirectory, relativePath), out var fullPath))
            {
                return items;
            }
            
            if (!Directory.Exists(fullPath))
            {
                return items;
            }
            
            try
            {
                await Task.Run(() =>
                {
                    items.AddRange(Directory.GetDirectories(fullPath)
                        .Select(d => Path.GetRelativePath(_baseDirectory, d) + "/"));
                    items.AddRange(Directory.GetFiles(fullPath)
                        .Select(f => Path.GetRelativePath(_baseDirectory, f)));
                });
                
                LogOperation($"LIST: {relativePath} ({items.Count} items)");
            }
            catch (Exception ex)
            {
                LogOperation($"ERROR: List failed for {relativePath}: {ex.Message}");
            }
            
            return items;
        }
        
        /// <summary>
        /// Deletes a file within the sandbox.
        /// </summary>
        public async Task<FileOperationResult> DeleteFileAsync(string relativePath)
        {
            var result = new FileOperationResult();
            
            if (_accessLevel != FileAccessLevel.Full)
            {
                result.Success = false;
                result.Error = "Delete operation requires full access level";
                return result;
            }
            
            if (!ValidatePath(Path.Combine(_baseDirectory, relativePath), out var fullPath))
            {
                result.Success = false;
                result.Error = "Path validation failed";
                return result;
            }
            
            result.FilePath = fullPath;
            
            try
            {
                EmitThought($"⟐ Deleting file: {relativePath}");
                
                await Task.Run(() => File.Delete(fullPath));
                result.Success = true;
                
                LogOperation($"DELETE: {relativePath}");
                EmitThought($"◈ File deleted: {relativePath}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                LogOperation($"ERROR: Delete failed for {relativePath}: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a directory within the sandbox.
        /// </summary>
        public async Task<FileOperationResult> CreateDirectoryAsync(string relativePath)
        {
            var result = new FileOperationResult();
            
            if (_accessLevel == FileAccessLevel.ReadOnly)
            {
                result.Success = false;
                result.Error = "Write access not permitted";
                return result;
            }
            
            if (!ValidatePath(Path.Combine(_baseDirectory, relativePath), out var fullPath))
            {
                result.Success = false;
                result.Error = "Path validation failed";
                return result;
            }
            
            result.FilePath = fullPath;
            
            try
            {
                await Task.Run(() => Directory.CreateDirectory(fullPath));
                result.Success = true;
                
                LogOperation($"MKDIR: {relativePath}");
                EmitThought($"◈ Directory created: {relativePath}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                LogOperation($"ERROR: Create directory failed for {relativePath}: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets the operation log.
        /// </summary>
        public IReadOnlyList<string> GetOperationLog()
        {
            return _operationLog.AsReadOnly();
        }
        
        /// <summary>
        /// Sets the access level.
        /// </summary>
        public void SetAccessLevel(FileAccessLevel level)
        {
            _accessLevel = level;
            LogOperation($"ACCESS LEVEL CHANGED: {level}");
            EmitThought($"◈ File system access level: {level}");
        }
        
        private void LogOperation(string message)
        {
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            _operationLog.Add(entry);
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
