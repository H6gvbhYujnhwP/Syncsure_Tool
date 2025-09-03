namespace SyncSureAgent.Configuration;

public class AgentConfiguration
{
    public string LicenseKey { get; set; } = string.Empty;
    public int MaxDevices { get; set; } = 5;
    public ApiConfiguration Api { get; set; } = new();
    public AgentSettings Agent { get; set; } = new();
    public LoggingConfiguration Logging { get; set; } = new();
}

public class ApiConfiguration
{
    public string BaseUrl { get; set; } = "https://syncsure-backend.onrender.com";
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}

public class AgentSettings
{
    public int HeartbeatMinutes { get; set; } = 5;
    public int UpdateCheckHours { get; set; } = 24;
    public int LogRetentionDays { get; set; } = 14;
    public bool EnableAutoUpdate { get; set; } = true;
    public bool EnableOneDriveProbes { get; set; } = true;
}

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableEventLog { get; set; } = true;
    public bool EnableConsoleLogging { get; set; } = true;
    public int MaxFileSizeMB { get; set; } = 10;
    public int RetainedFileCount { get; set; } = 14;
}

