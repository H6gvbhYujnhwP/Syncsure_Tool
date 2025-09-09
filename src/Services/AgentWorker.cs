using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncSureAgent.Configuration;
using SyncSureAgent.Models;

namespace SyncSureAgent.Services;

public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly AgentConfiguration _config;
    private readonly DeviceIdentityService _deviceService;
    private readonly ApiClient _apiClient;
    private readonly OneDriveProbeService _probeService;
    private readonly UpdaterService _updaterService;
    private readonly ILicenseKeyService _licenseKeyService;
    
    private string? _bindingId;
    private bool _isBound = false;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private string? _resolvedLicenseKey;

    public AgentWorker(
        ILogger<AgentWorker> logger,
        IOptions<AgentConfiguration> config,
        DeviceIdentityService deviceService,
        ApiClient apiClient,
        OneDriveProbeService probeService,
        UpdaterService updaterService,
        ILicenseKeyService licenseKeyService)
    {
        _logger = logger;
        _config = config.Value;
        _deviceService = deviceService;
        _apiClient = apiClient;
        _probeService = probeService;
        _updaterService = updaterService;
        _licenseKeyService = licenseKeyService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncSure Agent Worker starting...");
        
        // Resolve license key from multiple sources
        var args = Environment.GetCommandLineArgs();
        _resolvedLicenseKey = _licenseKeyService.ResolveLicenseKey(args);
        
        if (string.IsNullOrEmpty(_resolvedLicenseKey))
        {
            _logger.LogError("License key not configured. Agent cannot start.");
            _logger.LogError("Please provide a license key via:");
            _logger.LogError("  - Command line: --license-key SYNC-XXXXXXXXXX-XXXXXXXX");
            _logger.LogError("  - Environment variable: SYNCSURE_LICENSE_KEY");
            _logger.LogError("  - Config file: C:\\ProgramData\\SyncSure\\config.json");
            _logger.LogError("  - Embedded resource: license.key");
            return;
        }

        _logger.LogInformation("Agent configured with license: {LicenseKey}, Max devices: {MaxDevices}", 
            MaskLicenseKey(_resolvedLicenseKey), _config.MaxDevices);

        // Initialize device identity
        await _deviceService.InitializeAsync();
        _logger.LogInformation("Device identity initialized: {DeviceHash}", 
            _deviceService.DeviceHash[..8] + "...");

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteMainLoop(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent worker stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in main loop");
                
                // Wait before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("SyncSure Agent Worker stopped");
    }

    private async Task ExecuteMainLoop(CancellationToken cancellationToken)
    {
        // Ensure device is bound to license
        if (!_isBound)
        {
            await TryBindDevice(cancellationToken);
        }

        // Send heartbeat if bound
        if (_isBound)
        {
            await SendHeartbeat(cancellationToken);
        }

        // Check for updates periodically
        if (_config.Agent.EnableAutoUpdate && 
            DateTime.UtcNow - _lastUpdateCheck > TimeSpan.FromHours(_config.Agent.UpdateCheckHours))
        {
            await CheckForUpdates(cancellationToken);
            _lastUpdateCheck = DateTime.UtcNow;
        }

        // Wait for next iteration
        var delay = TimeSpan.FromMinutes(_config.Agent.HeartbeatMinutes);
        _logger.LogDebug("Waiting {DelayMinutes} minutes until next heartbeat", delay.TotalMinutes);
        
        await Task.Delay(delay, cancellationToken);
    }

    private async Task TryBindDevice(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to bind device to license...");
            
            var bindRequest = new BindRequest
            {
                LicenseKey = _resolvedLicenseKey!,
                DeviceHash = _deviceService.DeviceHash,
                AgentVersion = GetAgentVersion(),
                DeviceName = Environment.MachineName,
                OperatingSystem = Environment.OSVersion.ToString(),
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };

            var response = await _apiClient.BindDeviceAsync(bindRequest, cancellationToken);
            
            if (response.Success)
            {
                _bindingId = response.BindingId;
                _isBound = true;
                
                _logger.LogInformation("Device bound successfully. Binding ID: {BindingId}, Seats used: {SeatsUsed}/{MaxDevices}",
                    _bindingId, response.SeatsUsed, response.MaxDevices);
            }
            else
            {
                _logger.LogWarning("Device binding failed: {Error}", response.Error);
                _isBound = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during device binding");
            _isBound = false;
        }
    }

    private async Task SendHeartbeat(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Sending heartbeat...");
            
            // Collect OneDrive metrics
            var oneDriveMetrics = await _probeService.CollectMetricsAsync(cancellationToken);
            
            // Collect system metrics
            var systemMetrics = CollectSystemMetrics();
            
            var heartbeat = new HeartbeatRequest
            {
                LicenseKey = _resolvedLicenseKey!,
                DeviceHash = _deviceService.DeviceHash,
                BindingId = _bindingId,
                AgentVersion = GetAgentVersion(),
                Timestamp = DateTime.UtcNow,
                Metrics = new AgentMetrics
                {
                    OneDrive = oneDriveMetrics,
                    System = systemMetrics
                }
            };

            var response = await _apiClient.SendHeartbeatAsync(heartbeat, cancellationToken);
            
            if (response.Success)
            {
                _logger.LogDebug("Heartbeat sent successfully");
                
                // Handle any commands from server
                if (response.Commands?.Any() == true)
                {
                    await ProcessServerCommands(response.Commands, cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("Heartbeat failed: {Error}", response.Error);
                
                // If license is invalid, try to rebind
                if (response.Error?.Contains("license", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogInformation("License issue detected, will attempt to rebind");
                    _isBound = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during heartbeat");
        }
    }

    private SystemMetrics CollectSystemMetrics()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            
            return new SystemMetrics
            {
                CpuUsagePercent = 0, // TODO: Implement CPU usage calculation
                MemoryUsageMB = (long)(process.WorkingSet64 / (1024 * 1024)),
                DiskFreeSpaceGB = (long)GetDiskFreeSpace(),
                UptimeHours = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalHours
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
            return new SystemMetrics();
        }
    }

    private double GetDiskFreeSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:");
            return drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0); // Convert to GB
        }
        catch
        {
            return 0;
        }
    }

    private async Task ProcessServerCommands(IEnumerable<ServerCommand> commands, CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            try
            {
                _logger.LogInformation("Processing server command: {CommandType}", command.Type);
                
                switch (command.Type.ToLowerInvariant())
                {
                    case "update":
                        await _updaterService.CheckAndUpdateAsync(cancellationToken);
                        break;
                        
                    case "restart":
                        _logger.LogInformation("Restart command received, stopping service");
                        Environment.Exit(0);
                        break;
                        
                    case "unbind":
                        _logger.LogInformation("Unbind command received");
                        _isBound = false;
                        _bindingId = null;
                        break;
                        
                    default:
                        _logger.LogWarning("Unknown command type: {CommandType}", command.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process command: {CommandType}", command.Type);
            }
        }
    }

    private async Task CheckForUpdates(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking for updates...");
            await _updaterService.CheckAndUpdateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during update check");
        }
    }

    private string GetAgentVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    }

    private string MaskLicenseKey(string licenseKey)
    {
        if (string.IsNullOrEmpty(licenseKey) || licenseKey.Length < 8)
            return "****";
            
        return licenseKey[..4] + "****" + licenseKey[^4..];
    }
}

