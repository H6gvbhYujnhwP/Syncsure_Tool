using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncSureAgent.Configuration;
using System.Reflection;
using System.Text.Json;

namespace SyncSureAgent.Services;

public interface ILicenseKeyService
{
    string? ResolveLicenseKey(string[] args);
}

public class LicenseKeyService : ILicenseKeyService
{
    private readonly ILogger<LicenseKeyService> _logger;
    private readonly AgentConfiguration _config;

    public LicenseKeyService(ILogger<LicenseKeyService> logger, IOptions<AgentConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public string? ResolveLicenseKey(string[] args)
    {
        // Priority order for license key resolution:
        // 1. Command line argument (--license-key)
        // 2. Environment variable (SYNCSURE_LICENSE_KEY)
        // 3. Configuration file (appsettings.json or config.json)
        // 4. Embedded resource (license.key)

        // 1. Check command line arguments
        var licenseKey = GetLicenseKeyFromArgs(args);
        if (!string.IsNullOrEmpty(licenseKey))
        {
            _logger.LogInformation("License key resolved from command line argument");
            return licenseKey;
        }

        // 2. Check environment variable
        licenseKey = Environment.GetEnvironmentVariable("SYNCSURE_LICENSE_KEY");
        if (!string.IsNullOrEmpty(licenseKey))
        {
            _logger.LogInformation("License key resolved from environment variable");
            return licenseKey;
        }

        // 3. Check configuration file
        licenseKey = GetLicenseKeyFromConfig();
        if (!string.IsNullOrEmpty(licenseKey))
        {
            _logger.LogInformation("License key resolved from configuration file");
            return licenseKey;
        }

        // 4. Check embedded resource
        licenseKey = GetLicenseKeyFromEmbeddedResource();
        if (!string.IsNullOrEmpty(licenseKey))
        {
            _logger.LogInformation("License key resolved from embedded resource");
            return licenseKey;
        }

        _logger.LogError("No license key found in any source (command line, environment, config file, or embedded resource)");
        return null;
    }

    private string? GetLicenseKeyFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--license-key", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-l", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private string? GetLicenseKeyFromConfig()
    {
        // First check the injected configuration
        if (!string.IsNullOrEmpty(_config.LicenseKey))
        {
            return _config.LicenseKey;
        }

        // Then check external config files
        var configPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SyncSure", "config.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SyncSure", "appsettings.Production.json"),
            Path.Combine(Environment.CurrentDirectory, "config.json"),
            Path.Combine(Environment.CurrentDirectory, "appsettings.Production.json")
        };

        foreach (var configPath in configPaths)
        {
            if (File.Exists(configPath))
            {
                try
                {
                    var configJson = File.ReadAllText(configPath);
                    var configData = JsonSerializer.Deserialize<JsonElement>(configJson);
                    
                    if (configData.TryGetProperty("LicenseKey", out var licenseKeyElement))
                    {
                        var licenseKey = licenseKeyElement.GetString();
                        if (!string.IsNullOrEmpty(licenseKey))
                        {
                            _logger.LogInformation("License key found in config file: {ConfigPath}", configPath);
                            return licenseKey;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read config file: {ConfigPath}", configPath);
                }
            }
        }

        return null;
    }

    private string? GetLicenseKeyFromEmbeddedResource()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "SyncSureAgent.license.key";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var licenseKey = reader.ReadToEnd().Trim();
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    return licenseKey;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No embedded license key resource found");
        }

        // Also check for license.key file in the same directory as the executable
        try
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            var licenseFile = Path.Combine(exeDir, "license.key");
            
            if (File.Exists(licenseFile))
            {
                var licenseKey = File.ReadAllText(licenseFile).Trim();
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    _logger.LogInformation("License key found in license.key file");
                    return licenseKey;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read license.key file");
        }

        return null;
    }
}

