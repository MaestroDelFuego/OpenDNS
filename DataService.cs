using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

public class DataService
{
    private readonly string _dataDir = "data";
    private readonly string _blockedPath = "data/blocked_DNS.json";
    private readonly string _allowedPath = "data/allowed_IPs.json";
    private readonly string _subcategoryPath = "data/subcategory_config.json";

    public ConcurrentDictionary<string, List<string>> BlockedDomains { get; private set; }
    public ConcurrentDictionary<string, bool> SubcategoryConfig { get; private set; }
    public HashSet<string> AllowedIPs { get; private set; }

    public DataService()
    {
        BlockedDomains = LoadJsonAsync<ConcurrentDictionary<string, List<string>>>(_blockedPath).Result;
        SubcategoryConfig = LoadJsonAsync<ConcurrentDictionary<string, bool>>(_subcategoryPath).Result;
        AllowedIPs = new HashSet<string>(LoadJsonAsync<List<string>>(_allowedPath).Result);
        WatchFiles();
    }

    public void AddAllowedIP(string ip)
    {
        if (!IsValidIp(ip))
        {
            throw new ArgumentException("Invalid IP address.");
        }

        AllowedIPs.Add(ip);
        SaveJsonAsync(_allowedPath, AllowedIPs.ToList());
    }

    public void RemoveAllowedIP(string ip)
    {
        AllowedIPs.Remove(ip);
        SaveJsonAsync(_allowedPath, AllowedIPs.ToList());
    }

    public void UpdateSubcategoryConfig(Dictionary<string, bool> config)
    {
        SaveJsonAsync(_subcategoryPath, config);
    }

    public object GetCurrentConfig() => new
    {
        subcategoryConfig = SubcategoryConfig,
        allowedIPs = AllowedIPs.ToArray(),
        blockedDomains = BlockedDomains
    };

    private async Task<T> LoadJsonAsync<T>(string path)
    {
        if (!File.Exists(path)) return Activator.CreateInstance<T>();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json) ?? Activator.CreateInstance<T>();
    }

    private async Task SaveJsonAsync<T>(string path, T data)
    {
        var jsonContent = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, jsonContent);
    }

    private void WatchFiles()
    {
        var watcher = new FileSystemWatcher(_dataDir);
        watcher.Changed += (s, e) =>
        {
            if (e.Name == Path.GetFileName(_blockedPath)) BlockedDomains = LoadJsonAsync<ConcurrentDictionary<string, List<string>>>(_blockedPath).Result;
            else if (e.Name == Path.GetFileName(_subcategoryPath)) SubcategoryConfig = LoadJsonAsync<ConcurrentDictionary<string, bool>>(_subcategoryPath).Result;
            else if (e.Name == Path.GetFileName(_allowedPath)) AllowedIPs = new HashSet<string>(LoadJsonAsync<List<string>>(_allowedPath).Result);
        };
        watcher.EnableRaisingEvents = true;
    }

    private bool IsValidIp(string ip)
    {
        return System.Net.IPAddress.TryParse(ip, out _);
    }
}
