using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;

[ApiController]
[Route("api")]
public class DashboardController : ControllerBase
{
    private readonly DataService _dataService;

    public DashboardController(DataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        try
        {
            return Ok(_dataService.GetCurrentConfig());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the configuration.", error = ex.Message });
        }
    }

    [HttpPost("save-subcategories")]
    public IActionResult SaveSubcategories([FromBody] Dictionary<string, bool> config)
    {
        try
        {
            _dataService.UpdateSubcategoryConfig(config);
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while saving the subcategory configuration.", error = ex.Message });
        }
    }

    [HttpPost("add-ip")]
    public IActionResult AddIP([FromBody] string ip)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out _))
            {
                return BadRequest(new { message = "Invalid IP address format." });
            }

            _dataService.AddAllowedIP(ip);
            return Ok(new { status = "success" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while adding the IP.", error = ex.Message });
        }
    }

    [HttpPost("remove-ip")]
    public IActionResult RemoveIP([FromBody] string ip)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out _))
            {
                return BadRequest(new { message = "Invalid IP address format." });
            }

            _dataService.RemoveAllowedIP(ip);
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while removing the IP.", error = ex.Message });
        }
    }

    [HttpPost("test")]
    public IActionResult Test()
    {
        try
        {
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while Testing.", error = ex.Message });
        }
    }
}
