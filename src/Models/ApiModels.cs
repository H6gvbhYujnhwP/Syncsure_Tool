namespace SyncSureAgent.Models;

public interface IApiResponse
{
    bool Success { get; set; }
    string? Error { get; set; }
}

// Bind Device Request/Response
public class BindRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string DeviceHash { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = "windows";
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = "x64";
}

public class BindResponse : IApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BindingId { get; set; }
    public int MaxDevices { get; set; }
    public int CurrentDevices { get; set; }
    public int SeatsUsed { get; set; }
    public bool LicenseValid { get; set; }
    public DateTime? LicenseExpiresUtc { get; set; }
}

// Heartbeat Request/Response
public class HeartbeatRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string? BindingId { get; set; }
    public string DeviceHash { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public AgentMetrics Metrics { get; set; } = new();
    public OneDriveMetrics OneDriveMetrics { get; set; } = new();
    public SystemMetrics SystemMetrics { get; set; } = new();
}

public class HeartbeatResponse : IApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool DeviceStillBound { get; set; }
    public bool LicenseValid { get; set; }
    public DateTime? NextHeartbeatUtc { get; set; }
    public string? UpdateAvailable { get; set; }
    public string? Message { get; set; }
    public ServerCommand[]? Commands { get; set; }
}

// Update Check Request/Response
public class UpdateCheckResponse : IApiResponse
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public bool UpdateAvailable { get; set; }
    public string? LatestVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Sha256Hash { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool ForceUpdate { get; set; }
}

// OneDrive Metrics
public class OneDriveMetrics
{
    public string Status { get; set; } = "unknown"; // ok, warn, error, unknown
    public string? SyncStatus { get; set; } // synced, syncing, error, paused, unknown
    public bool ProcessRunning { get; set; }
    public DateTime? ProcessStartTime { get; set; }
    public long ProcessMemoryMB { get; set; }
    public string? OneDrivePath { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public long TotalSizeMB { get; set; }
    public string[]? Errors { get; set; }
}

// System Metrics
public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long MemoryTotalMB { get; set; }
    public long DiskFreeSpaceGB { get; set; }
    public long DiskTotalSpaceGB { get; set; }
    public DateTime SystemStartTimeUtc { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public string? ComputerName { get; set; }
    public string? UserName { get; set; }
    public bool IsNetworkConnected { get; set; }
    public string[]? NetworkInterfaces { get; set; }
}

// Configuration Models
public class AgentStatus
{
    public bool IsRunning { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public string Status { get; set; } = "unknown"; // starting, running, stopping, stopped, error
    public string? LastError { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime? NextHeartbeatUtc { get; set; }
    public bool DeviceBound { get; set; }
    public bool LicenseValid { get; set; }
    public string? DeviceId { get; set; }
    public string? LicenseKey { get; set; }
    public OneDriveMetrics? LastOneDriveMetrics { get; set; }
    public SystemMetrics? LastSystemMetrics { get; set; }
}


// Server Command Models
public class ServerCommand
{
    public string Type { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public string? CommandId { get; set; }
}


// Agent Metrics
public class AgentMetrics
{
    public OneDriveMetrics OneDrive { get; set; } = new();
    public SystemMetrics System { get; set; } = new();
    public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;
}

