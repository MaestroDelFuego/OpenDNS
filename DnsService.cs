using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DnsService
{
    private readonly ConcurrentDictionary<string, List<string>> _blockedDomains;
    private readonly ConcurrentDictionary<string, bool> _subcategoryConfig;
    private readonly HashSet<string> _allowedIPs;
    private readonly UdpClient _udpClient = new(53);

    public DnsService(
        ConcurrentDictionary<string, List<string>> blockedDomains,
        ConcurrentDictionary<string, bool> subcategoryConfig,
        HashSet<string> allowedIPs)
    {
        _blockedDomains = blockedDomains;
        _subcategoryConfig = subcategoryConfig;
        _allowedIPs = allowedIPs;
    }

    public void Start()
    {
        // Start DNS server in a background task
        Task.Run(async () =>
        {
            Console.WriteLine("🚀 DNS server listening on port 53...");
            while (true)
            {
                var result = await _udpClient.ReceiveAsync();
                var requesterIP = result.RemoteEndPoint.Address.ToString();

                // Check if the request is from an allowed IP
                //if (!_allowedIPs.Contains(requesterIP))
                //{
                //    Console.WriteLine($"⛔ Unauthorized DNS request from {requesterIP}");
                //    continue;
                //}

                // Parse the DNS query and get the domain
                string domain = ParseQuery(result.Buffer);
                Console.WriteLine($"🔍 DNS Query: {domain} from {requesterIP}");

                // Check if the domain is blocked
                if (domain == "google.com")
                {
                    var blockedResponse = BuildResponse(result.Buffer, "127.0.0.1");
                    await _udpClient.SendAsync(blockedResponse, blockedResponse.Length, result.RemoteEndPoint);
                }
                else
                {
                    // Forward the request to Google DNS and get the result
                    byte[] dnsResult = await ForwardToGoogleDns(result.Buffer);
                    await _udpClient.SendAsync(dnsResult, dnsResult.Length, result.RemoteEndPoint);
                }
            }
        });
    }

    private bool IsBlocked(string domain)
    {
        domain = domain.ToLower(); // Ensure domain comparison is case insensitive
        foreach (var entry in _blockedDomains)
        {
            // Skip blocked domains where the subcategory is not enabled
            if (_subcategoryConfig.TryGetValue(entry.Key, out var isBlocked) && !isBlocked)
            {
                foreach (var blocked in entry.Value)
                {
                    // Handle wildcard blocking (e.g. *.example.com)
                    if (blocked.StartsWith("*.") && domain.EndsWith(blocked[2..]))
                    {
                        Console.WriteLine($"Blocked wildcard domain: {domain}");
                        return true;
                    }

                    // Handle exact matches
                    if (domain == blocked)
                    {
                        Console.WriteLine($"Blocked domain: {domain}");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private async Task<byte[]> ForwardToGoogleDns(byte[] query)
    {
        using var forwarder = new UdpClient();
        // Send the query to Google's DNS server
        await forwarder.SendAsync(query, query.Length, "8.8.8.8", 53);
        // Wait for the response from Google DNS
        var response = await forwarder.ReceiveAsync();
        return response.Buffer;
    }

    // Parse the DNS query from the UDP packet buffer
    private string ParseQuery(byte[] buffer)
    {
        // For simplicity, assume the query starts at byte index 12 (DNS header)
        var domainParts = new List<string>();
        int index = 12;
        while (buffer[index] != 0) // 0 indicates end of domain part
        {
            byte length = buffer[index];
            domainParts.Add(Encoding.ASCII.GetString(buffer, index + 1, length));
            index += length + 1;
        }
        return string.Join(".", domainParts);
    }

    // Build a DNS response with a given IP (0.0.0.0 for blocked)
    private byte[] BuildResponse(byte[] query, string ip)
    {
        // DNS header: 12 bytes
        byte[] response = new byte[query.Length + 16];
        Array.Copy(query, response, query.Length);

        // DNS Response Section: Add 16 bytes for the response
        // Transaction ID (2 bytes), Flags (2 bytes), Question Count (2 bytes)
        // Answer Count (2 bytes), Authority Count (2 bytes), Additional Count (2 bytes)
        response[2] = 0x81; // Flags (Response, No Error)
        response[3] = 0x80;
        response[7] = 0x01; // One answer

        // Answer: Name (2 bytes), Type (2 bytes), Class (2 bytes), TTL (4 bytes), Data Length (2 bytes)
        int offset = query.Length;
        response[offset] = 0xC0; // Pointer to the domain name (relative to the start)
        response[offset + 1] = 0x0C; // Pointer offset (12 bytes from the start)

        // Type (A record)
        response[offset + 2] = 0x00;
        response[offset + 3] = 0x01; // Type A

        // Class (IN)
        response[offset + 4] = 0x00;
        response[offset + 5] = 0x01; // Class IN

        // TTL (Time To Live)
        response[offset + 6] = 0x00;
        response[offset + 7] = 0x00;
        response[offset + 8] = 0x00;
        response[offset + 9] = 0x1E; // 30 seconds TTL

        // Data Length
        response[offset + 10] = 0x00;
        response[offset + 11] = 0x04; // IPv4 address length

        // IP address (e.g., 0.0.0.0 for blocked)
        var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
        Array.Copy(ipBytes, 0, response, offset + 12, ipBytes.Length);

        return response;
    }
}
