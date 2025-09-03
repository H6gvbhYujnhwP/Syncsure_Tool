using Microsoft.Extensions.Logging;
using SyncSureAgent.Models;
using System.Diagnostics;
using System.Management;

namespace SyncSureAgent.Services;

public class OneDriveProbeService
{
    private readonly ILogger<OneDriveProbeService> _logger;

    public OneDriveProbeService(ILogger<OneDriveProbeService> logger)
    {
        _logger = logger;
    }

    public async Task<OneDriveMetrics> CollectMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Collecting OneDrive metrics...");

            var metrics = new OneDriveMetrics();

            // Check OneDrive process status
            await CheckOneDriveProcessAsync(metrics, cancellationToken);

            // Check OneDrive sync status
            await CheckSyncStatusAsync(metrics, cancellationToken);

            // Check OneDrive folders
            await CheckOneDriveFoldersAsync(metrics, cancellationToken);

            // Check for common OneDrive issues
            await CheckCommonIssuesAsync(metrics, cancellationToken);

            _logger.LogDebug("OneDrive metrics collected: Status={Status}, LastSuccess={LastSuccess}", 
                metrics.Status, metrics.LastSuccessUtc);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect OneDrive metrics");
            return new OneDriveMetrics
            {
                Status = "error",
                Errors = new[] { $"Metrics collection failed: {ex.Message}" }
            };
        }
    }

    private async Task CheckOneDriveProcessAsync(OneDriveMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            var oneDriveProcesses = Process.GetProcessesByName("OneDrive");
            
            if (oneDriveProcesses.Length > 0)
            {
                metrics.ProcessRunning = true;
                
                // Get the main OneDrive process
                var mainProcess = oneDriveProcesses
                    .OrderByDescending(p => p.StartTime)
                    .FirstOrDefault();

                if (mainProcess != null)
                {
                    metrics.ProcessStartTime = mainProcess.StartTime.ToUniversalTime();
                    metrics.ProcessMemoryMB = mainProcess.WorkingSet64 / (1024 * 1024);
                    
                    _logger.LogDebug("OneDrive process found: PID={ProcessId}, Memory={MemoryMB}MB, Started={StartTime}", 
                        mainProcess.Id, metrics.ProcessMemoryMB, metrics.ProcessStartTime);
                }
            }
            else
            {
                metrics.ProcessRunning = false;
                metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                    .Append("OneDrive process not running")
                    .ToArray();
                
                _logger.LogWarning("OneDrive process not found");
            }

            // Clean up process objects
            foreach (var process in oneDriveProcesses)
            {
                process.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check OneDrive process status");
            metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                .Append($"Process check failed: {ex.Message}")
                .ToArray();
        }
    }

    private async Task CheckSyncStatusAsync(OneDriveMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            // Check OneDrive registry keys for sync status
            var syncStatus = GetOneDriveSyncStatus();
            
            if (syncStatus.HasValue)
            {
                switch (syncStatus.Value)
                {
                    case 0:
                        metrics.SyncStatus = "synced";
                        metrics.Status = "ok";
                        metrics.LastSuccessUtc = DateTime.UtcNow;
                        break;
                    case 1:
                        metrics.SyncStatus = "syncing";
                        metrics.Status = "syncing";
                        break;
                    case 2:
                        metrics.SyncStatus = "error";
                        metrics.Status = "error";
                        metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                            .Append("OneDrive sync error detected")
                            .ToArray();
                        break;
                    case 3:
                        metrics.SyncStatus = "paused";
                        metrics.Status = "warn";
                        break;
                    default:
                        metrics.SyncStatus = "unknown";
                        metrics.Status = "warn";
                        break;
                }
            }
            else
            {
                // Fallback: check if OneDrive folder is accessible and recently modified
                await CheckOneDriveFolderActivity(metrics, cancellationToken);
            }

            _logger.LogDebug("OneDrive sync status: {SyncStatus} (code: {StatusCode})", 
                metrics.SyncStatus, syncStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check OneDrive sync status");
            metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                .Append($"Sync status check failed: {ex.Message}")
                .ToArray();
        }
    }

    private int? GetOneDriveSyncStatus()
    {
        try
        {
            // Try to get sync status from registry
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\OneDrive");
            var status = key?.GetValue("SyncStatus");
            
            if (status != null && int.TryParse(status.ToString(), out int statusCode))
            {
                return statusCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read OneDrive sync status from registry");
        }

        return null;
    }

    private async Task CheckOneDriveFolderActivity(OneDriveMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            var oneDrivePath = GetOneDrivePath();
            
            if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
            {
                metrics.OneDrivePath = oneDrivePath;
                
                // Check if folder has been modified recently (within last hour)
                var lastWrite = Directory.GetLastWriteTimeUtc(oneDrivePath);
                var hourAgo = DateTime.UtcNow.AddHours(-1);
                
                if (lastWrite > hourAgo)
                {
                    metrics.LastSuccessUtc = lastWrite;
                    metrics.Status = metrics.Status ?? "ok";
                }
                else
                {
                    // Check subdirectories for recent activity
                    var recentActivity = Directory.GetDirectories(oneDrivePath)
                        .Take(10) // Check first 10 subdirectories
                        .Select(dir => Directory.GetLastWriteTimeUtc(dir))
                        .Where(time => time > hourAgo)
                        .OrderByDescending(time => time)
                        .FirstOrDefault();

                    if (recentActivity > DateTime.MinValue)
                    {
                        metrics.LastSuccessUtc = recentActivity;
                        metrics.Status = metrics.Status ?? "ok";
                    }
                    else
                    {
                        metrics.Status = metrics.Status ?? "warn";
                        metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                            .Append("No recent OneDrive activity detected")
                            .ToArray();
                    }
                }

                _logger.LogDebug("OneDrive folder check: Path={Path}, LastWrite={LastWrite}", 
                    oneDrivePath, lastWrite);
            }
            else
            {
                metrics.Status = "error";
                metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                    .Append("OneDrive folder not found or inaccessible")
                    .ToArray();
                
                _logger.LogWarning("OneDrive folder not found: {Path}", oneDrivePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check OneDrive folder activity");
        }
    }

    private string? GetOneDrivePath()
    {
        try
        {
            // Try to get OneDrive path from registry
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\OneDrive");
            var userFolder = key?.GetValue("UserFolder")?.ToString();
            
            if (!string.IsNullOrEmpty(userFolder) && Directory.Exists(userFolder))
            {
                return userFolder;
            }

            // Fallback: check common OneDrive locations
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive - Personal"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive - Business")
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get OneDrive path");
        }

        return null;
    }

    private async Task CheckOneDriveFoldersAsync(OneDriveMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(metrics.OneDrivePath))
            {
                metrics.OneDrivePath = GetOneDrivePath();
            }

            if (!string.IsNullOrEmpty(metrics.OneDrivePath) && Directory.Exists(metrics.OneDrivePath))
            {
                // Count files and folders
                var fileCount = Directory.GetFiles(metrics.OneDrivePath, "*", SearchOption.AllDirectories).Length;
                var folderCount = Directory.GetDirectories(metrics.OneDrivePath, "*", SearchOption.AllDirectories).Length;
                
                metrics.FileCount = fileCount;
                metrics.FolderCount = folderCount;

                // Calculate total size (sample first 1000 files to avoid performance issues)
                var files = Directory.GetFiles(metrics.OneDrivePath, "*", SearchOption.AllDirectories)
                    .Take(1000);
                
                long totalSize = 0;
                foreach (var file in files)
                {
                    try
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
                
                metrics.TotalSizeMB = totalSize / (1024 * 1024);

                _logger.LogDebug("OneDrive folder stats: Files={FileCount}, Folders={FolderCount}, Size={SizeMB}MB", 
                    fileCount, folderCount, metrics.TotalSizeMB);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check OneDrive folders");
        }
    }

    private async Task CheckCommonIssuesAsync(OneDriveMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            var issues = new List<string>();

            // Check for OneDrive error notifications in Event Log
            try
            {
                using var eventLog = new EventLog("Application");
                var recentEntries = eventLog.Entries.Cast<EventLogEntry>()
                    .Where(e => e.Source.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
                    .Where(e => e.TimeGenerated > DateTime.Now.AddHours(-24))
                    .Where(e => e.EntryType == EventLogEntryType.Error || e.EntryType == EventLogEntryType.Warning)
                    .Take(5)
                    .ToList();

                foreach (var entry in recentEntries)
                {
                    issues.Add($"Event Log: {entry.EntryType} - {entry.Message.Substring(0, Math.Min(100, entry.Message.Length))}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to check OneDrive event log entries");
            }

            // Check for storage quota issues
            if (!string.IsNullOrEmpty(metrics.OneDrivePath))
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(metrics.OneDrivePath) ?? "C:");
                    var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    
                    if (freeSpaceGB < 1.0) // Less than 1GB free
                    {
                        issues.Add($"Low disk space: {freeSpaceGB:F1}GB free");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to check disk space");
                }
            }

            // Check for network connectivity
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync("https://onedrive.live.com", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    issues.Add($"OneDrive connectivity issue: HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Network connectivity issue: {ex.Message}");
            }

            if (issues.Any())
            {
                metrics.Errors = (metrics.Errors ?? Array.Empty<string>())
                    .Concat(issues)
                    .ToArray();
                
                if (metrics.Status == "ok")
                {
                    metrics.Status = "warn";
                }
            }

            _logger.LogDebug("OneDrive issues check completed: {IssueCount} issues found", issues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for common OneDrive issues");
        }
    }
}

