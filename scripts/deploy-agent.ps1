# SyncSure Agent Deployment Script
# This script automates the deployment of SyncSure Agent with license key embedding

param(
    [Parameter(Mandatory=$true)]
    [string]$LicenseKey,
    
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\SyncSure Agent",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "SyncSureAgent",
    
    [Parameter(Mandatory=$false)]
    [switch]$Uninstall,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

# Ensure running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Exiting..."
    exit 1
}

# Configuration
$LogPath = "$env:TEMP\SyncSureAgent-Deploy.log"
$DownloadUrl = "https://github.com/H6gvbhYujnhwP/Syncsure_Tool/releases/latest/download/SyncSureAgent.exe"
$TempExePath = "$env:TEMP\SyncSureAgent.exe"

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage
}

# Function to stop and remove existing service
function Remove-ExistingService {
    Write-Log "Checking for existing SyncSure Agent service..."
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Log "Found existing service. Stopping and removing..."
        
        if ($service.Status -eq "Running") {
            Stop-Service -Name $ServiceName -Force
            Write-Log "Service stopped."
        }
        
        # Remove service
        sc.exe delete $ServiceName
        Write-Log "Service removed."
        
        # Wait for service to be fully removed
        Start-Sleep -Seconds 3
    }
}

# Function to download latest agent
function Download-Agent {
    Write-Log "Downloading latest SyncSure Agent..."
    
    try {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempExePath -UseBasicParsing
        Write-Log "Download completed successfully."
        return $true
    }
    catch {
        Write-Log "Failed to download agent: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Function to embed license key
function Embed-LicenseKey {
    param([string]$ExePath, [string]$License)
    
    Write-Log "Embedding license key into executable..."
    
    try {
        # Read the executable as bytes
        $bytes = [System.IO.File]::ReadAllBytes($ExePath)
        
        # Find the license placeholder (32 character string of zeros)
        $placeholder = "00000000000000000000000000000000"
        $placeholderBytes = [System.Text.Encoding]::UTF8.GetBytes($placeholder)
        
        # Convert license to 32-character padded string
        $paddedLicense = $License.PadRight(32, '0').Substring(0, 32)
        $licenseBytes = [System.Text.Encoding]::UTF8.GetBytes($paddedLicense)
        
        # Find and replace placeholder
        $found = $false
        for ($i = 0; $i -le ($bytes.Length - $placeholderBytes.Length); $i++) {
            $match = $true
            for ($j = 0; $j -lt $placeholderBytes.Length; $j++) {
                if ($bytes[$i + $j] -ne $placeholderBytes[$j]) {
                    $match = $false
                    break
                }
            }
            
            if ($match) {
                # Replace placeholder with license
                for ($j = 0; $j -lt $licenseBytes.Length; $j++) {
                    $bytes[$i + $j] = $licenseBytes[$j]
                }
                $found = $true
                break
            }
        }
        
        if (-not $found) {
            Write-Log "License placeholder not found in executable" "WARNING"
            return $false
        }
        
        # Write modified executable
        [System.IO.File]::WriteAllBytes($ExePath, $bytes)
        Write-Log "License key embedded successfully."
        return $true
    }
    catch {
        Write-Log "Failed to embed license key: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Function to install agent
function Install-Agent {
    Write-Log "Installing SyncSure Agent to $InstallPath..."
    
    try {
        # Create installation directory
        if (-not (Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }
        
        # Copy executable
        $targetExePath = Join-Path $InstallPath "SyncSureAgent.exe"
        Copy-Item $TempExePath $targetExePath -Force
        
        # Create service
        Write-Log "Creating Windows service..."
        $serviceArgs = @(
            "create"
            $ServiceName
            "binPath= `"$targetExePath`""
            "start= auto"
            "DisplayName= `"SyncSure Agent`""
            "description= `"SyncSure Agent for OneDrive monitoring and synchronization`""
        )
        
        $result = & sc.exe @serviceArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create service: $result"
        }
        
        # Start service
        Write-Log "Starting SyncSure Agent service..."
        Start-Service -Name $ServiceName
        
        Write-Log "SyncSure Agent installed and started successfully."
        return $true
    }
    catch {
        Write-Log "Failed to install agent: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Function to uninstall agent
function Uninstall-Agent {
    Write-Log "Uninstalling SyncSure Agent..."
    
    try {
        # Remove service
        Remove-ExistingService
        
        # Remove installation directory
        if (Test-Path $InstallPath) {
            Remove-Item $InstallPath -Recurse -Force
            Write-Log "Installation directory removed."
        }
        
        Write-Log "SyncSure Agent uninstalled successfully."
        return $true
    }
    catch {
        Write-Log "Failed to uninstall agent: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Main execution
try {
    Write-Log "Starting SyncSure Agent deployment script..."
    Write-Log "Parameters: LicenseKey=$LicenseKey, InstallPath=$InstallPath, ServiceName=$ServiceName, Uninstall=$Uninstall, Force=$Force"
    
    if ($Uninstall) {
        # Uninstall mode
        if (Uninstall-Agent) {
            Write-Log "Uninstallation completed successfully."
            exit 0
        } else {
            Write-Log "Uninstallation failed." "ERROR"
            exit 1
        }
    }
    
    # Validate license key
    if ([string]::IsNullOrWhiteSpace($LicenseKey)) {
        Write-Log "License key is required for installation." "ERROR"
        exit 1
    }
    
    # Remove existing installation if Force is specified
    if ($Force) {
        Remove-ExistingService
        if (Test-Path $InstallPath) {
            Remove-Item $InstallPath -Recurse -Force
        }
    }
    
    # Check if already installed
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and -not $Force) {
        Write-Log "SyncSure Agent is already installed. Use -Force to reinstall." "WARNING"
        exit 0
    }
    
    # Download agent
    if (-not (Download-Agent)) {
        exit 1
    }
    
    # Embed license key
    if (-not (Embed-LicenseKey -ExePath $TempExePath -License $LicenseKey)) {
        Write-Log "Continuing installation without license embedding..." "WARNING"
    }
    
    # Install agent
    if (Install-Agent) {
        Write-Log "Deployment completed successfully."
        
        # Cleanup
        if (Test-Path $TempExePath) {
            Remove-Item $TempExePath -Force
        }
        
        Write-Log "Installation log saved to: $LogPath"
        exit 0
    } else {
        Write-Log "Deployment failed." "ERROR"
        exit 1
    }
}
catch {
    Write-Log "Unexpected error: $($_.Exception.Message)" "ERROR"
    exit 1
}
finally {
    # Cleanup temp files
    if (Test-Path $TempExePath) {
        Remove-Item $TempExePath -Force -ErrorAction SilentlyContinue
    }
}

