using Remotely.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class BrandingController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<BrandingController> _logger;

    public BrandingController(
        IOrganizationService organizationService,
        ILogger<BrandingController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    [HttpGet("{organizationId}")]
    public async Task<ActionResult<BrandingInfo>> Get(string organizationId)
    {
        var result = await _organizationService.GetBrandingInfo(organizationId);
        _logger.LogResult(result);
        if (!result.IsSuccess)
        {
            return NotFound();
        }
        return result.Value;
    }

    [HttpGet]
    public async Task<BrandingInfo> GetDefault()
    {
        var orgResult = await _organizationService.GetDefaultOrganization();
        _logger.LogResult(orgResult);

        if (!orgResult.IsSuccess)
        {
            return new();
        }

        var brandingResult = await _organizationService.GetBrandingInfo(orgResult.Value.ID);
        _logger.LogResult(brandingResult);

        if (!orgResult.IsSuccess || 
            brandingResult.Value is null)
        {
            return new();
        }

        return brandingResult.Value;
    }
}
