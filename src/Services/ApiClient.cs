using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SyncSureAgent.Configuration;
using SyncSureAgent.Models;
using System.Text;

namespace SyncSureAgent.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly AgentConfiguration _config;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, IOptions<AgentConfiguration> config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config.Value;
        
        _httpClient.BaseAddress = new Uri(_config.Api.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.Api.TimeoutSeconds);
    }

    public async Task<BindResponse> BindDeviceAsync(BindRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Attempting to bind device {DeviceHash} to license {LicenseKey}", 
                request.DeviceHash[..8] + "...", MaskLicenseKey(request.LicenseKey));

            var response = await PostWithRetryAsync<BindResponse>("/api/bind", request, cancellationToken);
            
            if (response.Success)
            {
                _logger.LogInformation("Device bound successfully. Binding ID: {BindingId}", response.BindingId);
            }
            else
            {
                _logger.LogWarning("Device binding failed: {Error}", response.Error);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during device binding");
            return new BindResponse { Success = false, Error = $"Exception: {ex.Message}" };
        }
    }

    public async Task<HeartbeatResponse> SendHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending heartbeat for device {DeviceHash}", request.DeviceHash[..8] + "...");

            var response = await PostWithRetryAsync<HeartbeatResponse>("/api/heartbeat", request, cancellationToken);
            
            if (response.Success)
            {
                _logger.LogDebug("Heartbeat sent successfully");
            }
            else
            {
                _logger.LogWarning("Heartbeat failed: {Error}", response.Error);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during heartbeat");
            return new HeartbeatResponse { Success = false, Error = $"Exception: {ex.Message}" };
        }
    }

    public async Task<UpdateCheckResponse> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking for updates, current version: {CurrentVersion}", currentVersion);

            var url = $"/api/agent/latest?arch=win-x64&current={currentVersion}";
            var response = await GetWithRetryAsync<UpdateCheckResponse>(url, cancellationToken);
            
            if (response.UpdateAvailable)
            {
                _logger.LogInformation("Update available: {LatestVersion} (current: {CurrentVersion})", 
                    response.LatestVersion, currentVersion);
            }
            else
            {
                _logger.LogDebug("No updates available");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during update check");
            return new UpdateCheckResponse { UpdateAvailable = false, Error = $"Exception: {ex.Message}" };
        }
    }

    private async Task<T> PostWithRetryAsync<T>(string endpoint, object request, CancellationToken cancellationToken) where T : new()
    {
        var json = JsonConvert.SerializeObject(request, Formatting.None);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        for (int attempt = 1; attempt <= _config.Api.RetryAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug("API call attempt {Attempt}/{MaxAttempts}: POST {Endpoint}", 
                    attempt, _config.Api.RetryAttempts, endpoint);

                var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<T>(responseContent) ?? new T();
                    _logger.LogDebug("API call successful: POST {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("API call failed: POST {Endpoint}, Status: {StatusCode}, Response: {Response}", 
                        endpoint, response.StatusCode, responseContent);

                    // Don't retry on client errors (4xx)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        var errorResult = new T();
                        if (errorResult is IApiResponse apiResponse)
                        {
                            apiResponse.Success = false;
                            apiResponse.Error = $"HTTP {response.StatusCode}: {responseContent}";
                        }
                        return errorResult;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("API call cancelled: POST {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API call attempt {Attempt} failed: POST {Endpoint}", attempt, endpoint);
            }

            // Wait before retry (except on last attempt)
            if (attempt < _config.Api.RetryAttempts)
            {
                var delay = TimeSpan.FromSeconds(_config.Api.RetryDelaySeconds * attempt);
                _logger.LogDebug("Waiting {DelaySeconds}s before retry", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All attempts failed
        var failedResult = new T();
        if (failedResult is IApiResponse apiResponse2)
        {
            apiResponse2.Success = false;
            apiResponse2.Error = $"All {_config.Api.RetryAttempts} attempts failed";
        }
        return failedResult;
    }

    private async Task<T> GetWithRetryAsync<T>(string endpoint, CancellationToken cancellationToken) where T : new()
    {
        for (int attempt = 1; attempt <= _config.Api.RetryAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug("API call attempt {Attempt}/{MaxAttempts}: GET {Endpoint}", 
                    attempt, _config.Api.RetryAttempts, endpoint);

                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<T>(responseContent) ?? new T();
                    _logger.LogDebug("API call successful: GET {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("API call failed: GET {Endpoint}, Status: {StatusCode}, Response: {Response}", 
                        endpoint, response.StatusCode, responseContent);

                    // Don't retry on client errors (4xx)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        var errorResult = new T();
                        if (errorResult is IApiResponse apiResponse)
                        {
                            apiResponse.Success = false;
                            apiResponse.Error = $"HTTP {response.StatusCode}: {responseContent}";
                        }
                        return errorResult;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("API call cancelled: GET {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API call attempt {Attempt} failed: GET {Endpoint}", attempt, endpoint);
            }

            // Wait before retry (except on last attempt)
            if (attempt < _config.Api.RetryAttempts)
            {
                var delay = TimeSpan.FromSeconds(_config.Api.RetryDelaySeconds * attempt);
                _logger.LogDebug("Waiting {DelaySeconds}s before retry", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All attempts failed
        var failedResult = new T();
        if (failedResult is IApiResponse apiResponse2)
        {
            apiResponse2.Success = false;
            apiResponse2.Error = $"All {_config.Api.RetryAttempts} attempts failed";
        }
        return failedResult;
    }

    private string MaskLicenseKey(string licenseKey)
    {
        if (string.IsNullOrEmpty(licenseKey) || licenseKey.Length < 8)
            return "****";
            
        return licenseKey[..4] + "****" + licenseKey[^4..];
    }
}

