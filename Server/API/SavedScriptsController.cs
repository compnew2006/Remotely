using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class SavedScriptsController : ControllerBase
{
    private readonly ISavedScriptService _savedScriptService;

    public SavedScriptsController(ISavedScriptService savedScriptService)
    {
        _savedScriptService = savedScriptService;
    }

    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [HttpGet("{scriptId}")]
    public async Task<ActionResult<SavedScript>> GetScript(Guid scriptId)
    {
        var result =  await _savedScriptService.GetSavedScript(scriptId);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        return result.Value;
    }
}
