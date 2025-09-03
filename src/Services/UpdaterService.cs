using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SyncSureAgent.Services;

public class UpdaterService
{
    private readonly ILogger<UpdaterService> _logger;
    private readonly ApiClient _apiClient;

    public UpdaterService(ILogger<UpdaterService> logger, ApiClient apiClient)
    {
        _logger = logger;
        _apiClient = apiClient;
    }

    public async Task CheckAndUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            _logger.LogInformation("Checking for updates, current version: {CurrentVersion}", currentVersion);

            var updateCheck = await _apiClient.CheckForUpdatesAsync(currentVersion, cancellationToken);
            
            if (!updateCheck.UpdateAvailable)
            {
                _logger.LogDebug("No updates available");
                return;
            }

            if (string.IsNullOrEmpty(updateCheck.DownloadUrl))
            {
                _logger.LogWarning("Update available but no download URL provided");
                return;
            }

            _logger.LogInformation("Update available: {LatestVersion}, downloading from {DownloadUrl}", 
                updateCheck.LatestVersion, updateCheck.DownloadUrl);

            await DownloadAndInstallUpdateAsync(updateCheck, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
        }
    }

    private async Task DownloadAndInstallUpdateAsync(Models.UpdateCheckResponse updateInfo, CancellationToken cancellationToken)
    {
        string? tempFilePath = null;
        
        try
        {
            // Create temporary directory for update
            var tempDir = Path.Combine(Path.GetTempPath(), "SyncSureUpdate", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            tempFilePath = Path.Combine(tempDir, "SyncSureAgent.exe");
            
            _logger.LogInformation("Downloading update to {TempPath}", tempFilePath);

            // Download the update
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10); // Longer timeout for downloads
            
            using var response = await httpClient.GetAsync(updateInfo.DownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fileStream, cancellationToken);
            
            _logger.LogInformation("Update downloaded successfully, size: {SizeKB} KB", 
                new FileInfo(tempFilePath).Length / 1024);

            // Verify hash if provided
            if (!string.IsNullOrEmpty(updateInfo.Sha256Hash))
            {
                if (!await VerifyFileHashAsync(tempFilePath, updateInfo.Sha256Hash))
                {
                    _logger.LogError("Update file hash verification failed, aborting update");
                    return;
                }
                
                _logger.LogInformation("Update file hash verified successfully");
            }
            else
            {
                _logger.LogWarning("No hash provided for update verification");
            }

            // Install the update
            await InstallUpdateAsync(tempFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and install update");
        }
        finally
        {
            // Clean up temporary file
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    var tempDir = Path.GetDirectoryName(tempFilePath);
                    if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary update files");
                }
            }
        }
    }

    private async Task<bool> VerifyFileHashAsync(string filePath, string expectedHash)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            var hashBytes = await sha256.ComputeHashAsync(fileStream);
            var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var expectedHashLower = expectedHash.ToLowerInvariant();
            
            var isValid = actualHash == expectedHashLower;
            
            _logger.LogDebug("Hash verification: Expected={ExpectedHash}, Actual={ActualHash}, Valid={IsValid}", 
                expectedHashLower, actualHash, isValid);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify file hash");
            return false;
        }
    }

    private async Task InstallUpdateAsync(string updateFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var currentExePath = Environment.ProcessPath ?? 
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyncSureAgent.exe");
            
            var backupPath = currentExePath + ".backup";
            
            _logger.LogInformation("Installing update: {UpdateFile} -> {CurrentFile}", updateFilePath, currentExePath);

            // Create backup of current executable
            if (File.Exists(currentExePath))
            {
                File.Copy(currentExePath, backupPath, true);
                _logger.LogDebug("Created backup: {BackupPath}", backupPath);
            }

            // Create update script
            var updateScript = CreateUpdateScript(updateFilePath, currentExePath, backupPath);
            var scriptPath = Path.Combine(Path.GetTempPath(), "syncsure-update.bat");
            
            await File.WriteAllTextAsync(scriptPath, updateScript, cancellationToken);
            
            _logger.LogInformation("Starting update process, service will restart automatically");

            // Start the update script and exit
            var processInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(processInfo);
            
            // Give the script a moment to start
            await Task.Delay(1000, cancellationToken);
            
            // Exit the current process to allow update
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install update");
        }
    }

    private string CreateUpdateScript(string updateFilePath, string currentExePath, string backupPath)
    {
        return $@"@echo off
echo SyncSure Agent Update Script
echo ============================

echo Waiting for service to stop...
timeout /t 5 /nobreak >nul

echo Stopping SyncSure Agent service...
sc stop SyncSureAgent >nul 2>&1
timeout /t 3 /nobreak >nul

echo Installing update...
copy ""{updateFilePath}"" ""{currentExePath}"" >nul
if %ERRORLEVEL% NEQ 0 (
    echo Update failed, restoring backup...
    copy ""{backupPath}"" ""{currentExePath}"" >nul
    goto :start_service
)

echo Update installed successfully
del ""{backupPath}"" >nul 2>&1

:start_service
echo Starting SyncSure Agent service...
sc start SyncSureAgent >nul 2>&1

echo Cleaning up...
del ""{updateFilePath}"" >nul 2>&1
del ""%~f0"" >nul 2>&1

echo Update process completed
";
    }

    private string GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    }
}

