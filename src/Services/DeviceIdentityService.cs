using Microsoft.Extensions.Logging;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace SyncSureAgent.Services;

public class DeviceIdentityService
{
    private readonly ILogger<DeviceIdentityService> _logger;
    private string? _deviceHash;
    private readonly string _deviceHashPath;

    public DeviceIdentityService(ILogger<DeviceIdentityService> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SyncSure");
        _deviceHashPath = Path.Combine(dataDir, "device.hash");
    }

    public string DeviceHash => _deviceHash ?? throw new InvalidOperationException("Device identity not initialized");

    public async Task InitializeAsync()
    {
        try
        {
            // Try to load existing device hash
            if (File.Exists(_deviceHashPath))
            {
                _deviceHash = await File.ReadAllTextAsync(_deviceHashPath);
                _logger.LogDebug("Loaded existing device hash from {Path}", _deviceHashPath);
            }
            else
            {
                // Generate new device hash
                _deviceHash = await GenerateDeviceHashAsync();
                
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(_deviceHashPath)!);
                
                // Save device hash
                await File.WriteAllTextAsync(_deviceHashPath, _deviceHash);
                _logger.LogInformation("Generated new device hash and saved to {Path}", _deviceHashPath);
            }

            _logger.LogInformation("Device identity initialized: {DeviceHashPrefix}...", _deviceHash[..8]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize device identity");
            throw;
        }
    }

    private async Task<string> GenerateDeviceHashAsync()
    {
        try
        {
            var identifiers = new List<string>();
            
            // Get machine GUID (most reliable)
            try
            {
                var machineGuid = GetMachineGuid();
                if (!string.IsNullOrEmpty(machineGuid))
                {
                    identifiers.Add($"MACHINE:{machineGuid}");
                    _logger.LogDebug("Added machine GUID to device identity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get machine GUID");
            }

            // Get CPU information
            try
            {
                var cpuId = GetCpuId();
                if (!string.IsNullOrEmpty(cpuId))
                {
                    identifiers.Add($"CPU:{cpuId}");
                    _logger.LogDebug("Added CPU ID to device identity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get CPU ID");
            }

            // Get motherboard serial
            try
            {
                var motherboardSerial = GetMotherboardSerial();
                if (!string.IsNullOrEmpty(motherboardSerial))
                {
                    identifiers.Add($"MB:{motherboardSerial}");
                    _logger.LogDebug("Added motherboard serial to device identity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get motherboard serial");
            }

            // Get primary disk serial
            try
            {
                var diskSerial = GetPrimaryDiskSerial();
                if (!string.IsNullOrEmpty(diskSerial))
                {
                    identifiers.Add($"DISK:{diskSerial}");
                    _logger.LogDebug("Added disk serial to device identity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get disk serial");
            }

            // Fallback: use machine name and user name
            if (identifiers.Count == 0)
            {
                identifiers.Add($"FALLBACK:{Environment.MachineName}:{Environment.UserName}");
                _logger.LogWarning("Using fallback identifiers for device hash");
            }

            // Combine all identifiers
            var combined = string.Join("|", identifiers);
            _logger.LogDebug("Device identifiers collected: {Count} items", identifiers.Count);

            // Add salt and hash
            var saltedInput = $"SYNCSURE_SALT_2025|{combined}";
            var hash = ComputeSha256Hash(saltedInput);
            
            return hash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate device hash");
            throw;
        }
    }

    private string GetMachineGuid()
    {
        try
        {
            // Try registry first (most reliable)
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid")?.ToString();
            
            if (!string.IsNullOrEmpty(guid))
                return guid;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get machine GUID from registry");
        }

        // Fallback to WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                var uuid = obj["UUID"]?.ToString();
                if (!string.IsNullOrEmpty(uuid) && uuid != "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                    return uuid;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get UUID from WMI");
        }

        return string.Empty;
    }

    private string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                var processorId = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(processorId))
                    return processorId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get CPU ID");
        }

        return string.Empty;
    }

    private string GetMotherboardSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                var serial = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serial) && serial.Trim() != "")
                    return serial;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get motherboard serial");
        }

        return string.Empty;
    }

    private string GetPrimaryDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                var serial = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(serial))
                    return serial;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get disk serial");
        }

        return string.Empty;
    }

    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

