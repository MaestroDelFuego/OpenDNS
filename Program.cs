using Newtonsoft.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Load configuration from the 'data' folder where 'appsettings.json' is stored
builder.Configuration.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "data"))
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Log to verify configuration loading
var allowedIps = config.GetSection("AdminSettings:AllowedIPs").Get<List<string>>();
Console.WriteLine("Allowed IPs: " + string.Join(", ", allowedIps));  // Logging for debugging

// Register the required collections in the DI container
builder.Services.AddSingleton(new ConcurrentDictionary<string, List<string>>()); // Blocked domains
builder.Services.AddSingleton(new ConcurrentDictionary<string, bool>()); // Subcategory config
builder.Services.AddSingleton(new HashSet<string>(allowedIps)); // Allowed IPs (loaded from config)
builder.Services.AddSingleton<DataService>(); // Data service for file operations

// Register other services
builder.Services.AddSingleton<DnsService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Automatically serve static files
app.UseStaticFiles();

// Automatically create necessary directories and files on startup
var logger = app.Services.GetRequiredService<ILogger<Program>>(); // Get ILogger from DI container
SetupFilesAndFolders(logger); // Pass the logger to the SetupFilesAndFolders method

// 🔐 Apply IP restriction middleware BEFORE routing
app.UseMiddleware<IpRestrictionMiddleware>(); // Ensure middleware is added to the pipeline

app.UseRouting();
app.MapControllers(); // Ensure controllers are mapped

// Start DNS server in background
var dnsService = app.Services.GetRequiredService<DnsService>();
dnsService.Start();

app.Run(); // Start web server


// Method to setup folders and files if they don't exist
void SetupFilesAndFolders(ILogger logger)
{
    try
    {
        // Define the data directory path
        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");

        // Ensure the directory exists
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
            logger.LogInformation($"Directory created: {dataDir}");
        }
        else
        {
            logger.LogInformation($"Directory already exists: {dataDir}");
        }

        // Files to check and create with default content if missing
        var files = new Dictionary<string, object>
        {
            { "blocked_domains.json", new { Subcategories = new string[] { } } },
            { "allowed_ips.json", new string[] { } },
            { "subcategory_config.json", new { SubcategorySettings = new Dictionary<string, bool>() } },
            { "appsettings.json", new
                {
                    AdminSettings = new
                    {
                        AllowedIPs = new string[] { "127.0.0.1" }
                    }
                }
            }
        };

        foreach (var file in files)
        {
            var filePath = Path.Combine(dataDir, file.Key);

            try
            {
                // If the file doesn't exist, create it with default content
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(file.Value, Newtonsoft.Json.Formatting.Indented));
                    logger.LogInformation($"Created missing file: {filePath}");
                }
                else
                {
                    logger.LogInformation($"File already exists: {filePath}");
                }
            }
            catch (IOException ex)
            {
                logger.LogError(ex, $"IOException occurred while creating file: {filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, $"UnauthorizedAccessException while accessing file: {filePath}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while creating file: {filePath}");
            }
        }
        // Get local IP address
        string localIp = GetLocalIpAddress();
        logger.LogInformation($"DNS IP Address: {localIp}:53");
        logger.LogInformation($"WEB INTERFACE IP Address: {localIp}:56825");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while setting up files and folders.");
    }
}

string GetLocalIpAddress()
{
    foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
    {
        if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
            continue;

        var ipProps = ni.GetIPProperties();
        foreach (var addr in ipProps.UnicastAddresses)
        {
            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !System.Net.IPAddress.IsLoopback(addr.Address))
            {
                return addr.Address.ToString();
            }
        }
    }

    return "127.0.0.1"; // fallback
}
