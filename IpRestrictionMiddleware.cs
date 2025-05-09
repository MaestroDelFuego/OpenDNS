using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedIps;
    private readonly ILogger<IpRestrictionMiddleware> _logger;

    public IpRestrictionMiddleware(RequestDelegate next, IConfiguration config, ILogger<IpRestrictionMiddleware> logger)
    {
        _next = next;
        _allowedIps = config.GetSection("AdminSettings:AllowedIPs")
                           .Get<List<string>>()
                           ?.Select(ip => ip.Trim())
                           .ToHashSet() ?? new();
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestIp = context.Connection.RemoteIpAddress?.ToString();
        if (requestIp == "::1") requestIp = "127.0.0.1"; // Convert IPv6 loopback to IPv4

        _logger.LogInformation($"Request from IP: {requestIp}");

        if (IsRestrictedRoute(context.Request.Path))
        {
            if (_allowedIps.Contains(requestIp))
            {
                _logger.LogInformation($"Access granted to IP: {requestIp}");

                if (context.Request.Path == "/index.html")
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
                    if (File.Exists(filePath))
                    {
                        var content = await File.ReadAllTextAsync(filePath);
                        await context.Response.WriteAsync(content);
                    }
                    else
                    {
                        await context.Response.WriteAsync("Index file not found.");
                        _logger.LogError("Index file not found.");
                    }
                }
                else
                {
                     await HandleApiRequests(context); // Handle API-specific requests; // Continue the pipeline
                }
                if (context.Request.Path == "/")
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "blocked_domain.html");
                    if (File.Exists(filePath))
                    {
                        var content = await File.ReadAllTextAsync(filePath);
                        await context.Response.WriteAsync(content);
                    }
                    else
                    {
                        await context.Response.WriteAsync("Index file not found.");
                        _logger.LogError("Index file not found.");
                    }
                }
                else
                {
                    await HandleApiRequests(context); // Handle API-specific requests; // Continue the pipeline
                }

            }
            else
            {
                _logger.LogWarning($"Access denied to IP: {requestIp}");

                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "access-denied.html");
                if (File.Exists(filePath))
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    await context.Response.WriteAsync(content);
                }
                else
                {
                    await context.Response.WriteAsync("Access Denied: Your IP is not authorized.");
                }
            }
        }
        else
        {
            await _next(context); // Continue the pipeline
        }
    }

    private bool IsRestrictedRoute(string path)
    {
        return path.StartsWith("/api") || path == "/" || path == "/index.html";
    }
    private async Task HandleApiRequests(HttpContext context)
    {
        if (context.Request.Path.Equals("/api/subcategory_config", System.StringComparison.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "subcategory_config.json");
            if (File.Exists(filePath))
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jsonContent);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Subcategory config file not found.");
            }
        }
        else if (context.Request.Path.Equals("/api/allowed_ips", System.StringComparison.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "allowed_ips.json");
            if (File.Exists(filePath))
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jsonContent);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Allowed IPs file not found.");
            }
        }
        else if (context.Request.Path.Equals("/api/blocked_domains", System.StringComparison.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "blocked_domains.json");
            if (File.Exists(filePath))
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jsonContent);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Blocked domains file not found.");
            }
        }
        else
        {
            // If the route doesn't match the expected ones, continue with the request pipeline
            await _next(context);
        }
    }
}
