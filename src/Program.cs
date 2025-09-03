using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SyncSureAgent.Services;
using SyncSureAgent.Configuration;
using System.Diagnostics;

namespace SyncSureAgent;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Handle command line arguments for installation
        if (args.Length > 0 && args[0].Equals("/quiet", StringComparison.OrdinalIgnoreCase))
        {
            await HandleQuietInstall();
            return;
        }

        // Configure Serilog early for startup logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(GetDataDirectory(), "logs", "syncsure-agent-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.EventLog("SyncSure Agent", manageEventSource: true)
            .CreateLogger();

        try
        {
            Log.Information("Starting SyncSure Agent v{Version}", GetVersion());
            
            var builder = Host.CreateApplicationBuilder(args);
            
            // Configure services
            ConfigureServices(builder);
            
            // Build and run
            var host = builder.Build();
            
            Log.Information("SyncSure Agent started successfully");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SyncSure Agent failed to start");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(HostApplicationBuilder builder)
    {
        // Add Windows Service support
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "SyncSureAgent";
        });

        // Configure Serilog
        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(GetDataDirectory(), "logs", "syncsure-agent-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.EventLog("SyncSure Agent", manageEventSource: true)
            .Enrich.FromLogContext());

        // Add configuration
        builder.Services.Configure<AgentConfiguration>(
            builder.Configuration);

        // Add HTTP client
        builder.Services.AddHttpClient<ApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", $"SyncSureAgent/{GetVersion()}");
        });

        // Add services
        builder.Services.AddSingleton<DeviceIdentityService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<OneDriveProbeService>();
        builder.Services.AddSingleton<UpdaterService>();
        
        // Add the main worker
        builder.Services.AddHostedService<AgentWorker>();
    }

    private static async Task HandleQuietInstall()
    {
        try
        {
            Console.WriteLine("SyncSure Agent - Silent Installation");
            
            // Create data directory
            var dataDir = GetDataDirectory();
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(Path.Combine(dataDir, "logs"));
            
            // Move config file if it exists alongside the EXE
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            var sourceConfig = Path.Combine(exeDir, "appsettings.Production.json");
            var targetConfig = Path.Combine(dataDir, "appsettings.Production.json");
            
            if (File.Exists(sourceConfig) && !File.Exists(targetConfig))
            {
                File.Copy(sourceConfig, targetConfig);
                Console.WriteLine($"Configuration copied to: {targetConfig}");
            }
            
            Console.WriteLine("Silent installation completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Installation failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static string GetDataDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SyncSure");
    }

    private static string GetVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0.0";
    }
}

