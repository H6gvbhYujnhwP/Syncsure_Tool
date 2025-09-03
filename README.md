# SyncSure Agent

A Windows service-based monitoring agent for OneDrive synchronization status and device management.

## Overview

SyncSure Agent is a lightweight Windows service that continuously monitors OneDrive synchronization status, device identity, and provides real-time reporting to the SyncSure management platform. The agent operates silently in the background and provides comprehensive logging for troubleshooting and monitoring purposes.

## Features

- **OneDrive Monitoring**: Real-time monitoring of OneDrive sync status and health
- **Device Identity Management**: Automatic device registration and identity management
- **Comprehensive Logging**: Detailed logging with configurable levels for debugging
- **Automatic Updates**: Self-updating mechanism for seamless maintenance
- **License Management**: Embedded license key system for customer-specific deployments
- **Windows Service**: Runs as a Windows service for reliable background operation
- **API Integration**: Secure communication with SyncSure backend services

## System Requirements

- Windows 10 or Windows Server 2016 or later
- .NET 6.0 Runtime (automatically installed if missing)
- Administrator privileges for installation
- Internet connectivity for API communication
- OneDrive installed and configured

## Installation

### Automated Installation (Recommended)

Use the PowerShell deployment script for automated installation:

```powershell
# Download and run the deployment script
Invoke-WebRequest -Uri "https://github.com/H6gvbhYujnhwP/Syncsure_Tool/raw/main/scripts/deploy-agent.ps1" -OutFile "deploy-agent.ps1"

# Run with your license key
.\deploy-agent.ps1 -LicenseKey "YOUR_LICENSE_KEY_HERE"
```

### Manual Installation

1. Download the latest release from [GitHub Releases](https://github.com/H6gvbhYujnhwP/Syncsure_Tool/releases)
2. Extract the files to your desired installation directory
3. Run PowerShell as Administrator
4. Execute the installation script:

```powershell
.\deploy-agent.ps1 -LicenseKey "YOUR_LICENSE_KEY" -InstallPath "C:\Program Files\SyncSure Agent"
```

### Batch Deployment

For deploying to multiple machines, use the batch deployment script:

```powershell
# Deploy to multiple machines
.\batch-deploy.ps1 -LicenseKey "YOUR_LICENSE_KEY" -ComputerNames @("PC1", "PC2", "PC3") -MaxConcurrent 3
```

## Configuration

The agent uses a configuration file located at `%ProgramFiles%\SyncSure Agent\config.json`:

```json
{
  "ApiBaseUrl": "https://syncsure-backend.onrender.com",
  "CheckIntervalMinutes": 5,
  "LogLevel": "Information",
  "LogRetentionDays": 30,
  "DeviceIdentityFile": "device-identity.json",
  "LicenseKey": "embedded-in-executable"
}
```

### Configuration Options

- **ApiBaseUrl**: Backend API endpoint URL
- **CheckIntervalMinutes**: Interval between OneDrive status checks (default: 5 minutes)
- **LogLevel**: Logging level (Trace, Debug, Information, Warning, Error, Critical)
- **LogRetentionDays**: Number of days to retain log files (default: 30)
- **DeviceIdentityFile**: Path to device identity storage file

## Usage

### Service Management

The agent runs as a Windows service named "SyncSureAgent". Use standard Windows service management tools:

```powershell
# Start the service
Start-Service -Name "SyncSureAgent"

# Stop the service
Stop-Service -Name "SyncSureAgent"

# Check service status
Get-Service -Name "SyncSureAgent"

# View service logs
Get-EventLog -LogName Application -Source "SyncSureAgent" -Newest 50
```

### Command Line Options

When running the executable directly (for testing):

```cmd
SyncSureAgent.exe [options]

Options:
  --install          Install as Windows service
  --uninstall        Uninstall Windows service
  --console          Run in console mode (for debugging)
  --license <key>    Set license key
  --config <path>    Specify configuration file path
  --help             Show help information
```

## Logging

The agent provides comprehensive logging with multiple output targets:

### Log Locations

- **Service Logs**: Windows Event Log (Application log, source: SyncSureAgent)
- **File Logs**: `%ProgramFiles%\SyncSure Agent\logs\`
  - `syncsure-agent-YYYYMMDD.log`: Daily rotating log files
  - `syncsure-agent-errors.log`: Error-only log file

### Log Levels

- **Trace**: Detailed execution flow (development only)
- **Debug**: Detailed diagnostic information
- **Information**: General operational messages
- **Warning**: Potentially harmful situations
- **Error**: Error events that don't stop the application
- **Critical**: Critical errors that may cause termination

### Sample Log Entries

```
2024-01-15 10:30:15.123 [INFO] SyncSure Agent starting up...
2024-01-15 10:30:15.456 [INFO] Device identity loaded: DESKTOP-ABC123
2024-01-15 10:30:16.789 [INFO] OneDrive status check completed: Synchronized
2024-01-15 10:30:17.012 [INFO] API heartbeat sent successfully
2024-01-15 10:35:15.234 [DEBUG] OneDrive probe initiated
2024-01-15 10:35:15.567 [DEBUG] Registry check: HKCU\Software\Microsoft\OneDrive
2024-01-15 10:35:15.890 [INFO] OneDrive sync status: Up to date
```

## API Integration

The agent communicates with the SyncSure backend API for:

- Device registration and authentication
- Status reporting and heartbeat
- Configuration updates
- License validation
- Update notifications

### API Endpoints

- `POST /api/devices/register`: Device registration
- `POST /api/devices/heartbeat`: Status reporting
- `GET /api/devices/config`: Configuration retrieval
- `POST /api/devices/logs`: Log submission (if enabled)

## Troubleshooting

### Common Issues

#### Service Won't Start

1. Check Windows Event Log for error messages
2. Verify .NET 6.0 Runtime is installed
3. Ensure proper file permissions on installation directory
4. Validate license key format

```powershell
# Check service status
Get-Service -Name "SyncSureAgent"

# View recent errors
Get-EventLog -LogName Application -Source "SyncSureAgent" -EntryType Error -Newest 10
```

#### OneDrive Not Detected

1. Verify OneDrive is installed and running
2. Check if OneDrive is signed in
3. Review agent logs for OneDrive detection messages

```powershell
# Check OneDrive process
Get-Process -Name "OneDrive" -ErrorAction SilentlyContinue

# Check OneDrive registry entries
Get-ItemProperty -Path "HKCU:\Software\Microsoft\OneDrive" -ErrorAction SilentlyContinue
```

#### Network Connectivity Issues

1. Test internet connectivity
2. Check firewall settings
3. Verify API endpoint accessibility

```powershell
# Test API connectivity
Test-NetConnection -ComputerName "syncsure-backend.onrender.com" -Port 443

# Test DNS resolution
Resolve-DnsName "syncsure-backend.onrender.com"
```

### Debug Mode

Run the agent in console mode for debugging:

```cmd
# Stop the service first
net stop SyncSureAgent

# Run in console mode
"C:\Program Files\SyncSure Agent\SyncSureAgent.exe" --console --log-level Debug
```

### Log Analysis

Use PowerShell to analyze logs:

```powershell
# Search for errors in log files
Get-ChildItem "C:\Program Files\SyncSure Agent\logs\*.log" | 
    Select-String -Pattern "ERROR|CRITICAL" | 
    Select-Object -Last 20

# Monitor live log file
Get-Content "C:\Program Files\SyncSure Agent\logs\syncsure-agent-$(Get-Date -Format 'yyyyMMdd').log" -Wait -Tail 10
```

## Uninstallation

### Automated Uninstallation

```powershell
.\deploy-agent.ps1 -Uninstall
```

### Manual Uninstallation

1. Stop the service:
   ```cmd
   net stop SyncSureAgent
   ```

2. Remove the service:
   ```cmd
   sc delete SyncSureAgent
   ```

3. Delete installation directory:
   ```powershell
   Remove-Item "C:\Program Files\SyncSure Agent" -Recurse -Force
   ```

## Development

### Building from Source

Requirements:
- Visual Studio 2022 or .NET 6.0 SDK
- Windows 10/11 or Windows Server

```bash
# Clone the repository
git clone https://github.com/H6gvbhYujnhwP/Syncsure_Tool.git
cd Syncsure_Tool

# Build the project
dotnet build src/SyncSureAgent.csproj --configuration Release

# Run tests
dotnet test

# Publish for deployment
dotnet publish src/SyncSureAgent.csproj -c Release -r win-x64 --self-contained true
```

### Project Structure

```
src/
├── Program.cs                      # Main entry point
├── Configuration/
│   └── AgentConfiguration.cs       # Configuration management
├── Services/
│   ├── AgentWorker.cs              # Main service worker
│   ├── ApiClient.cs                # API communication
│   ├── DeviceIdentityService.cs    # Device identity management
│   ├── OneDriveProbeService.cs     # OneDrive monitoring
│   └── UpdaterService.cs           # Auto-update functionality
├── Models/
│   └── ApiModels.cs                # API data models
└── SyncSureAgent.csproj            # Project file

scripts/
├── deploy-agent.ps1                # Single machine deployment
└── batch-deploy.ps1               # Batch deployment

.github/workflows/
└── build.yml                      # GitHub Actions CI/CD
```

## Security

- License keys are embedded in the executable during build
- All API communication uses HTTPS/TLS encryption
- Device identity is generated using hardware fingerprinting
- Sensitive data is not logged or stored in plain text
- Regular security updates through auto-update mechanism

## Support

For technical support and bug reports:

1. Check the [GitHub Issues](https://github.com/H6gvbhYujnhwP/Syncsure_Tool/issues)
2. Review the troubleshooting section above
3. Submit detailed bug reports with log files
4. Contact support with your license key for priority assistance

## License

This software is proprietary and requires a valid license key for operation. Each license is tied to specific customer deployments and cannot be redistributed.

## Changelog

### Version 1.0.0 (Initial Release)
- OneDrive monitoring and status reporting
- Windows service implementation
- Comprehensive logging system
- API integration with SyncSure backend
- Automated deployment scripts
- License key embedding system

---

**Note**: This agent requires a valid SyncSure license key to operate. Contact your system administrator or SyncSure support for license key information.

