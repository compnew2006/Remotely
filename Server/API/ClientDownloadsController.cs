using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Extensions;
using Remotely.Shared.Models;
using Remotely.Shared.Services;
using System.Text;
using FileIO = System.IO.File;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class ClientDownloadsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IEmbeddedServerDataProvider _embeddedDataSearcher;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly IWebHostEnvironment _hostEnv;
    private readonly ILogger<ClientDownloadsController> _logger;
    private readonly ApiAuthorizationFilter _apiAuthorizationFilter;

    public ClientDownloadsController(
        IWebHostEnvironment hostEnv,
        IEmbeddedServerDataProvider embeddedDataSearcher,
        IDataService dataService,
        ILogger<ClientDownloadsController> logger,
        ApiAuthorizationFilter apiAuthorizationFilter)
    {
        _hostEnv = hostEnv;
        _embeddedDataSearcher = embeddedDataSearcher;
        _dataService = dataService;
        _logger = logger;
        _apiAuthorizationFilter = apiAuthorizationFilter;
    }

    [HttpGet("desktop/{platformID}")]
    public async Task<IActionResult> GetDesktop(string platformID)
    {
        return await GetDesktopInternal(platformID, null);
    }

    [HttpGet("desktop/{platformId}/{organizationId}")]
    public async Task<IActionResult> GetDesktop(string platformId, string organizationId)
    {
        return await GetDesktopInternal(platformId, organizationId);
    }

    private async Task<IActionResult> GetDesktopInternal(string platformID, string? organizationId)
    {
        var filePath = GetDesktopPath(platformID);

        if (filePath is null)
        {
            return NotFound();
        }

        // Quick Support clients are downloadable without an account. Anonymous
        // browser downloads redirect to the raw file; API/automation clients
        // still get 401 unless they authenticate or supply an API key.
        if (!await _apiAuthorizationFilter.IsAuthorizedAsync(HttpContext))
        {
            if (IsBrowserDownloadRequest())
            {
                var fullPath = Path.Combine(_hostEnv.WebRootPath, filePath);
                if (!FileIO.Exists(fullPath))
                {
                    return NotFound();
                }

                return Redirect("/" + filePath);
            }

            return Unauthorized();
        }

        return organizationId is null
            ? await GetDesktopFile(filePath)
            : await GetDesktopFile(filePath, organizationId);
    }

    private static string? GetDesktopPath(string platformID)
    {
        return platformID switch
        {
            "WindowsDesktop-x64" => "Content/Win-x64/Remotely_Desktop.exe",
            "WindowsDesktop-x86" => "Content/Win-x86/Remotely_Desktop.exe",
            "UbuntuDesktop" => "Content/Linux-x64/Remotely_Desktop",
            "MacOS-x64" => "Content/MacOS-x64/Remotely_Desktop",
            "MacOS-arm64" => "Content/MacOS-arm64/Remotely_Desktop",
            _ => null
        };
    }

    private bool IsBrowserDownloadRequest()
    {
        var accept = Request.Headers.Accept.ToString();
        var fetchMode = Request.Headers["Sec-Fetch-Mode"].ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
               accept.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
               fetchMode.Equals("navigate", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("Mozilla", StringComparison.OrdinalIgnoreCase);
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpGet("{platformID}")]
    public async Task<IActionResult> GetInstaller(string platformID)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }
        return await GetInstallFile(orgId, platformID);
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpGet("{platformId}/{organizationId}")]
    public async Task<IActionResult> GetInstaller(string platformId, string organizationId)
    {
        return await GetInstallFile(organizationId, platformId);
    }

    private async Task<IActionResult> GetBashInstaller(string fileName, string organizationId)
    {
        var fileContents = new List<string>();
        fileContents.AddRange(await FileIO.ReadAllLinesAsync(Path.Combine(_hostEnv.WebRootPath, "Content", fileName)));

        var hostIndex = fileContents.IndexOf("HostName=");
        var orgIndex = fileContents.IndexOf("Organization=");

        var settings = await _dataService.GetSettings();
        var effectiveScheme = settings.ForceClientHttps ? "https" : Request.Scheme;

        fileContents[hostIndex] = $"HostName=\"{effectiveScheme}://{Request.Host}\"";
        fileContents[orgIndex] = $"Organization=\"{organizationId}\"";
        var fileBytes = Encoding.UTF8.GetBytes(string.Join("\n", fileContents));
        return File(fileBytes, "application/octet-stream", fileName);
    }

    private async Task<IActionResult> GetDesktopFile(string relativeFilePath, string? organizationId = null)
    {
        await LogRequest(nameof(GetDesktopFile));
        var defaultOrg = await _dataService.GetDefaultOrganization();

        // The default org will be used if unspecified, so might as well save the
        // space in the file name.
        if (defaultOrg.IsSuccess && 
            defaultOrg.Value.ID.Equals(organizationId, StringComparison.OrdinalIgnoreCase))
        {
            organizationId = null;
        }

        var settings = await _dataService.GetSettings();
        var effectiveScheme = settings.ForceClientHttps ? "https" : Request.Scheme;
        var serverUrl = $"{effectiveScheme}://{Request.Host}";

        var embeddedData = new EmbeddedServerData(new Uri(serverUrl), organizationId);
        var fileName = _embeddedDataSearcher.GetEncodedFileName(relativeFilePath, embeddedData);
        return File(relativeFilePath, "application/octet-stream", fileName);
    }

    private async Task<IActionResult> GetInstallFile(string organizationId, string platformID)
    {
        var settings = await _dataService.GetSettings();
        await LogRequest(nameof(GetInstallFile));

        if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(15)))
        {
            return StatusCode(StatusCodes.Status408RequestTimeout);
        }

        try
        {
            switch (platformID)
            {
                case "WindowsInstaller":
                    {
                        var effectiveScheme = settings.ForceClientHttps ? "https" : Request.Scheme;

                        var filePath = Path.Combine(_hostEnv.WebRootPath, "Content", "Install-Remotely.ps1");
                        if (!FileIO.Exists(filePath))
                        {
                            return NotFound();
                        }
                        
                        var fileContents = await FileIO.ReadAllLinesAsync(filePath);
                        var hostIndex = fileContents.IndexWhere(x => 
                            x.Contains("[string]$HostName = $null", StringComparison.OrdinalIgnoreCase));
                        var orgIndex = fileContents.IndexWhere(x => 
                            x.Contains("[string]$Organization = $null", StringComparison.OrdinalIgnoreCase));

                        if (hostIndex < 0 || orgIndex < 0)
                        {
                            return NotFound();
                        }

                        fileContents[hostIndex] = $"[string]$HostName = \"{effectiveScheme}://{Request.Host}\"";
                        fileContents[orgIndex] = $"[string]$Organization = \"{organizationId}\"";
                        var fileBytes = Encoding.UTF8.GetBytes(string.Join("\n", fileContents));

                        return File(fileBytes, "application/octet-stream", "Install-Remotely.ps1");
                    }
                case "ManjaroInstaller-x64":
                    {
                        var fileName = "Install-Manjaro-x64.sh";

                        return await GetBashInstaller(fileName, organizationId);
                    }
                case "UbuntuInstaller-x64":
                    {
                        var fileName = "Install-Ubuntu-x64.sh";

                        return await GetBashInstaller(fileName, organizationId);
                    }
                case "MacOSInstaller-x64":
                    {
                        var fileName = "Install-MacOS-x64.sh";

                        return await GetBashInstaller(fileName, organizationId);
                    }
                case "MacOSInstaller-arm64":
                    {
                        var fileName = "Install-MacOS-arm64.sh";

                        return await GetBashInstaller(fileName, organizationId);
                    }
                default:
                    return BadRequest();
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task LogRequest(string methodName)
    {
        var settings = await _dataService.GetSettings();
        if (settings.UseHttpLogging)
        {
            var ip = Request.HttpContext.Connection.RemoteIpAddress;
            if (ip?.IsIPv4MappedToIPv6 == true)
            {
                ip = ip.MapToIPv4();
            }

            var effectiveScheme = settings.ForceClientHttps ? "https" : Request.Scheme;

            _logger.LogInformation(
                "Started client download via {methodName}.  Effective Scheme: {scheme}.  Effective Host: {host}.  Remote IP: {ip}.",
                methodName,
                effectiveScheme,
                Request.Host,
                $"{ip}");
        }
    }
}
