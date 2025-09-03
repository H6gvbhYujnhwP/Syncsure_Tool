# SyncSure Agent Batch Deployment Script
# This script deploys SyncSure Agent to multiple machines using PowerShell remoting

param(
    [Parameter(Mandatory=$true)]
    [string]$LicenseKey,
    
    [Parameter(Mandatory=$true)]
    [string[]]$ComputerNames,
    
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\SyncSure Agent",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "SyncSureAgent",
    
    [Parameter(Mandatory=$false)]
    [switch]$Force,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxConcurrent = 5,
    
    [Parameter(Mandatory=$false)]
    [string]$LogPath = "$env:TEMP\SyncSureAgent-BatchDeploy.log"
)

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage
}

# Function to deploy to a single machine
function Deploy-ToMachine {
    param(
        [string]$ComputerName,
        [string]$License,
        [string]$Path,
        [string]$Service,
        [bool]$ForceInstall
    )
    
    $result = @{
        ComputerName = $ComputerName
        Success = $false
        Message = ""
        StartTime = Get-Date
        EndTime = $null
    }
    
    try {
        Write-Log "Starting deployment to $ComputerName..."
        
        # Test connectivity
        if (-not (Test-Connection -ComputerName $ComputerName -Count 1 -Quiet)) {
            throw "Computer $ComputerName is not reachable"
        }
        
        # Check if WinRM is available
        $session = $null
        try {
            $session = New-PSSession -ComputerName $ComputerName -ErrorAction Stop
        }
        catch {
            throw "Failed to establish PowerShell session: $($_.Exception.Message)"
        }
        
        # Copy deployment script to remote machine
        $remoteScriptPath = "C:\Temp\deploy-agent.ps1"
        $localScriptPath = Join-Path $PSScriptRoot "deploy-agent.ps1"
        
        Copy-Item -Path $localScriptPath -Destination $remoteScriptPath -ToSession $session -Force
        
        # Execute deployment on remote machine
        $scriptBlock = {
            param($License, $Path, $Service, $ForceInstall, $ScriptPath)
            
            $params = @{
                LicenseKey = $License
                InstallPath = $Path
                ServiceName = $Service
            }
            
            if ($ForceInstall) {
                $params.Force = $true
            }
            
            & $ScriptPath @params
            return $LASTEXITCODE
        }
        
        $exitCode = Invoke-Command -Session $session -ScriptBlock $scriptBlock -ArgumentList $License, $Path, $Service, $ForceInstall, $remoteScriptPath
        
        if ($exitCode -eq 0) {
            $result.Success = $true
            $result.Message = "Deployment successful"
            Write-Log "Deployment to $ComputerName completed successfully."
        } else {
            throw "Deployment script returned exit code $exitCode"
        }
    }
    catch {
        $result.Success = $false
        $result.Message = $_.Exception.Message
        Write-Log "Deployment to $ComputerName failed: $($_.Exception.Message)" "ERROR"
    }
    finally {
        if ($session) {
            Remove-PSSession -Session $session
        }
        $result.EndTime = Get-Date
    }
    
    return $result
}

# Function to generate deployment report
function Generate-Report {
    param([array]$Results)
    
    $reportPath = "$env:TEMP\SyncSureAgent-BatchDeployReport-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>SyncSure Agent Batch Deployment Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        .success { background-color: #d4edda; }
        .failure { background-color: #f8d7da; }
        .summary { margin-bottom: 20px; padding: 10px; background-color: #e9ecef; }
    </style>
</head>
<body>
    <h1>SyncSure Agent Batch Deployment Report</h1>
    <div class="summary">
        <h2>Summary</h2>
        <p><strong>Total Machines:</strong> $($Results.Count)</p>
        <p><strong>Successful:</strong> $($Results | Where-Object {$_.Success} | Measure-Object | Select-Object -ExpandProperty Count)</p>
        <p><strong>Failed:</strong> $($Results | Where-Object {-not $_.Success} | Measure-Object | Select-Object -ExpandProperty Count)</p>
        <p><strong>Report Generated:</strong> $(Get-Date)</p>
    </div>
    
    <h2>Detailed Results</h2>
    <table>
        <tr>
            <th>Computer Name</th>
            <th>Status</th>
            <th>Message</th>
            <th>Start Time</th>
            <th>End Time</th>
            <th>Duration</th>
        </tr>
"@
    
    foreach ($result in $Results) {
        $statusClass = if ($result.Success) { "success" } else { "failure" }
        $status = if ($result.Success) { "SUCCESS" } else { "FAILED" }
        $duration = if ($result.EndTime) { 
            ($result.EndTime - $result.StartTime).TotalSeconds.ToString("F1") + "s" 
        } else { 
            "N/A" 
        }
        
        $html += @"
        <tr class="$statusClass">
            <td>$($result.ComputerName)</td>
            <td>$status</td>
            <td>$($result.Message)</td>
            <td>$($result.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))</td>
            <td>$($result.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))</td>
            <td>$duration</td>
        </tr>
"@
    }
    
    $html += @"
    </table>
</body>
</html>
"@
    
    $html | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Log "Deployment report generated: $reportPath"
    return $reportPath
}

# Main execution
try {
    Write-Log "Starting SyncSure Agent batch deployment..."
    Write-Log "Target machines: $($ComputerNames -join ', ')"
    Write-Log "License Key: $($LicenseKey.Substring(0, 8))..."
    Write-Log "Max concurrent deployments: $MaxConcurrent"
    
    # Validate parameters
    if ([string]::IsNullOrWhiteSpace($LicenseKey)) {
        throw "License key is required"
    }
    
    if ($ComputerNames.Count -eq 0) {
        throw "At least one computer name must be specified"
    }
    
    # Check if deployment script exists
    $deployScriptPath = Join-Path $PSScriptRoot "deploy-agent.ps1"
    if (-not (Test-Path $deployScriptPath)) {
        throw "Deployment script not found: $deployScriptPath"
    }
    
    # Initialize results array
    $results = @()
    $jobs = @()
    
    # Deploy to machines with concurrency control
    foreach ($computerName in $ComputerNames) {
        # Wait if we've reached max concurrent jobs
        while ($jobs.Count -ge $MaxConcurrent) {
            $completedJobs = $jobs | Where-Object { $_.State -eq "Completed" -or $_.State -eq "Failed" }
            
            foreach ($job in $completedJobs) {
                $result = Receive-Job -Job $job
                $results += $result
                Remove-Job -Job $job
                $jobs = $jobs | Where-Object { $_.Id -ne $job.Id }
            }
            
            if ($jobs.Count -ge $MaxConcurrent) {
                Start-Sleep -Seconds 1
            }
        }
        
        # Start deployment job
        $job = Start-Job -ScriptBlock ${function:Deploy-ToMachine} -ArgumentList $computerName, $LicenseKey, $InstallPath, $ServiceName, $Force.IsPresent
        $jobs += $job
        
        Write-Log "Started deployment job for $computerName (Job ID: $($job.Id))"
    }
    
    # Wait for all remaining jobs to complete
    Write-Log "Waiting for all deployment jobs to complete..."
    while ($jobs.Count -gt 0) {
        $completedJobs = $jobs | Where-Object { $_.State -eq "Completed" -or $_.State -eq "Failed" }
        
        foreach ($job in $completedJobs) {
            $result = Receive-Job -Job $job
            $results += $result
            Remove-Job -Job $job
            $jobs = $jobs | Where-Object { $_.Id -ne $job.Id }
        }
        
        if ($jobs.Count -gt 0) {
            Start-Sleep -Seconds 1
        }
    }
    
    # Generate and display summary
    $successCount = ($results | Where-Object { $_.Success }).Count
    $failureCount = ($results | Where-Object { -not $_.Success }).Count
    
    Write-Log "Batch deployment completed!"
    Write-Log "Total machines: $($results.Count)"
    Write-Log "Successful deployments: $successCount"
    Write-Log "Failed deployments: $failureCount"
    
    # Generate detailed report
    $reportPath = Generate-Report -Results $results
    
    # Display failed deployments
    if ($failureCount -gt 0) {
        Write-Log "Failed deployments:" "WARNING"
        $results | Where-Object { -not $_.Success } | ForEach-Object {
            Write-Log "  $($_.ComputerName): $($_.Message)" "WARNING"
        }
    }
    
    Write-Log "Batch deployment log: $LogPath"
    Write-Log "Detailed report: $reportPath"
    
    if ($failureCount -eq 0) {
        exit 0
    } else {
        exit 1
    }
}
catch {
    Write-Log "Batch deployment failed: $($_.Exception.Message)" "ERROR"
    exit 1
}

