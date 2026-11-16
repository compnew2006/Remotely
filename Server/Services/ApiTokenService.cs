using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The API tokens of the server: their lifecycle, and the validation that
/// every API request goes through.
/// </summary>
public interface IApiTokenService
{
    Task<Result<ApiToken>> CreateApiToken(string userName, string tokenName, string secretHash);

    Task<Result> DeleteApiToken(string userName, string tokenId);

    ApiToken[] GetAllApiTokens(string userId);

    Task<Result<ApiToken>> GetApiKey(string keyId);

    Task<Result> RenameApiToken(string userName, string tokenId, string tokenName);

    Task<bool> ValidateApiKey(string keyId, string apiSecret, string requestPath, string remoteIP);
}

public class ApiTokenService : IApiTokenService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<ApiTokenService> _logger;

    public ApiTokenService(
        IAppDbFactory appDbFactory,
        ILogger<ApiTokenService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result<ApiToken>> CreateApiToken(string userName, string tokenName, string secretHash)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail<ApiToken>("User not found.");
        }

        var newToken = new ApiToken()
        {
            Name = tokenName,
            OrganizationID = user.OrganizationID,
            Secret = secretHash
        };
        dbContext.ApiTokens.Add(newToken);
        await dbContext.SaveChangesAsync();
        return Result.Ok(newToken);
    }

    public async Task<Result> DeleteApiToken(string userName, string tokenId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var token = dbContext.ApiTokens.FirstOrDefault(x =>
            x.OrganizationID == user.OrganizationID &&
            x.ID == tokenId);

        if (token is null)
        {
            return Result.Fail("Token not found.");
        }

        dbContext.ApiTokens.Remove(token);
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public ApiToken[] GetAllApiTokens(string userId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = dbContext.Users.FirstOrDefault(x => x.Id == userId);

        if (user is null)
        {
            return Array.Empty<ApiToken>();
        }

        return dbContext.ApiTokens
            .AsNoTracking()
            .Where(x => x.OrganizationID == user.OrganizationID)
            .OrderByDescending(x => x.LastUsed)
            .ToArray();
    }

    public async Task<Result<ApiToken>> GetApiKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return Result.Fail<ApiToken>("Key ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var token = await dbContext.ApiTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == keyId);

        if (token is null)
        {
            return Result.Fail<ApiToken>("API key not found.");
        }

        return Result.Ok(token);
    }

    public async Task<Result> RenameApiToken(string userName, string tokenId, string tokenName)
    {
        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var token = await dbContext.ApiTokens.FirstOrDefaultAsync(x =>
            x.OrganizationID == user.OrganizationID &&
            x.ID == tokenId);

        if (token is null)
        {
            return Result.Fail("API token not found.");
        }

        token.Name = tokenName;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<bool> ValidateApiKey(string keyId, string apiSecret, string requestPath, string remoteIP)
    {
        using var dbContext = _appDbFactory.GetContext();

        var hasher = new PasswordHasher<string>();
        var token = await dbContext.ApiTokens.FirstOrDefaultAsync(x => x.ID == keyId);

        var isValid =
            !string.IsNullOrWhiteSpace(token?.Secret) &&
            hasher.VerifyHashedPassword(string.Empty, token.Secret, apiSecret) == PasswordVerificationResult.Success;

        // Only a successful authentication may write.  Updating LastUsed for a
        // failed attempt turned every rejected request into a database write,
        // so anyone who knew a key id could drive write load without a secret,
        // and the audit trail recorded rejected attempts as key usage.
        if (token is not null && isValid)
        {
            token.LastUsed = DateTimeOffset.Now;
            await dbContext.SaveChangesAsync();
        }

        _logger.LogInformation(
            "API token used.  Token: {keyId}.  Path: {requestPath}.  Validated: {isValid}.  Remote IP: {remoteIP}",
            keyId,
            requestPath,
            isValid,
            remoteIP);

        return isValid;
    }
}
