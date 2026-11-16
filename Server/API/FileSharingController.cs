using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class FileSharingController : ControllerBase
{
    private readonly ISharedFileService _sharedFileService;

    public FileSharingController(ISharedFileService sharedFileService)
    {
        _sharedFileService = sharedFileService;
    }

    [HttpGet("{id}")]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    public async Task<IActionResult> Get(string id)
    {
        var sharedFileResult = await _sharedFileService.GetSharedFiled(id);

        if (!sharedFileResult.IsSuccess)
        {
            return NotFound();
        }

        var sharedFile = sharedFileResult.Value;
        var contentType = sharedFile.ContentType ?? "application/octet-stream";
        return File(sharedFile.FileContents, contentType, sharedFile.FileName);
    }

    [HttpPost]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [RequestSizeLimit(AppConstants.MaxUploadFileSize)]
    public async Task<IEnumerable<string>> Post()
    {
        if (Request.Form.Files.Count !> 0)
        {
            return Array.Empty<string>();
        }

        var fileIds = new List<string>();

        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            orgId = string.Empty;
        }

        foreach (var file in Request.Form.Files)
        {
            var id = await _sharedFileService.AddSharedFile(file, orgId);
            fileIds.Add(id);
        }
        return fileIds;
    }
}
