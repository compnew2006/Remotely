using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The files that the users share with each other.
/// </summary>
public interface ISharedFileService
{
    Task<string> AddSharedFile(IBrowserFile file, string organizationId, Action<double, string> progressCallback);

    Task<string> AddSharedFile(IFormFile file, string organizationId);

    Task<Result<SharedFile>> GetSharedFiled(string fileId);
}

public class SharedFileService : ISharedFileService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SharedFileService> _logger;

    public SharedFileService(
        IAppDbFactory appDbFactory,
        ISettingsService settingsService,
        ILogger<SharedFileService> logger)
    {
        _appDbFactory = appDbFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<string> AddSharedFile(IBrowserFile file, string organizationId, Action<double, string> progressCallback)
    {
        var fileSize = file.Size;
        var fileName = file.Name;

        var fileContents = new byte[fileSize];
        var stream = file.OpenReadStream(AppConstants.MaxUploadFileSize);

        var bytesRead = 0;
        while (bytesRead < fileSize)
        {
            var segmentEnd = Math.Min(50_000, fileSize - bytesRead);
            var read = await stream.ReadAsync(fileContents.AsMemory(bytesRead, (int)segmentEnd));
            if (read == 0)
            {
                break;
            }
            bytesRead += read;
            progressCallback.Invoke((double)bytesRead / fileSize, fileName);
        }

        progressCallback.Invoke(1, fileName);

        return await AddSharedFileImpl(file.Name, fileContents, file.ContentType, organizationId);
    }

    public async Task<Result<SharedFile>> GetSharedFiled(string fileId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var file = await dbContext.SharedFiles.FindAsync(fileId);

        if (file is null)
        {
            return Result.Fail<SharedFile>("File not found.");
        }
        return Result.Ok(file);
    }

    private async Task<string> AddSharedFileImpl(
        string fileName,
        byte[] fileContents,
        string contentType,
        string organizationId)
    {
        var settings = await _settingsService.GetSettings();
        using var dbContext = _appDbFactory.GetContext();

        var expirationDate = DateTimeOffset.Now.AddDays(-settings.DataRetentionInDays);
        var expiredFiles = dbContext.SharedFiles.Where(x => x.Timestamp < expirationDate);
        dbContext.RemoveRange(expiredFiles);

        var sharedFile = new SharedFile()
        {
            FileContents = fileContents,
            FileName = fileName,
            ContentType = contentType,
            OrganizationID = organizationId
        };

        dbContext.SharedFiles.Add(sharedFile);

        await dbContext.SaveChangesAsync();
        return sharedFile.ID;
    }

    public async Task<string> AddSharedFile(IFormFile file, string organizationId)
    {
        var fileContents = new byte[file.Length];
        using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(fileContents.AsMemory());

        return await AddSharedFileImpl(file.Name, fileContents, file.ContentType, organizationId);
    }
}
