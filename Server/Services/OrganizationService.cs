using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// The organizations of the server and their branding.
/// </summary>
public interface IOrganizationService
{
    Task<Result<BrandingInfo>> GetBrandingInfo(string organizationId);

    Task<Result<Organization>> GetDefaultOrganization();

    Task<Result<Organization>> GetOrganizationById(string organizationId);

    Task<Result<Organization>> GetOrganizationByUserName(string userName);

    Task<Result<string>> GetOrganizationNameById(string organizationId);

    Task<Result<string>> GetOrganizationNameByUserName(string userName);

    int GetOrganizationCount();

    Task<int> GetOrganizationCountAsync();

    Task ResetBranding(string organizationId);

    Task SetIsDefaultOrganization(string orgID, bool isDefault);

    Task UpdateBrandingInfo(
        string organizationId,
        string productName,
        byte[] iconBytes);

    Task<Result> UpdateOrganizationName(string orgId, string newName);
}

public class OrganizationService : IOrganizationService
{
    private readonly IAppDbFactory _appDbFactory;

    public OrganizationService(IAppDbFactory appDbFactory)
    {
        _appDbFactory = appDbFactory;
    }

    public async Task<Result<Organization>> GetOrganizationById(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations.FindAsync(organizationId);

        if (org is null)
        {
            return Result.Fail<Organization>("Organization not found.");
        }
        return Result.Ok(org);
    }

    public async Task<Result<Organization>> GetOrganizationByUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<Organization>("User name is required.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.UserName!.ToLower() == userName.ToLower());

        if (user?.Organization is null)
        {
            return Result.Fail<Organization>("User not found.");
        }

        return Result.Ok(user.Organization);
    }

    public async Task<Result<string>> GetOrganizationNameById(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (org is null)
        {
            return Result.Fail<string>("Organization not found.");
        }

        return Result.Ok(org.OrganizationName);
    }

    public async Task<Result<string>> GetOrganizationNameByUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<string>("Username cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail<string>("User not found.");
        }

        var orgName = $"{user.Organization?.OrganizationName}";
        return Result.Ok(orgName);
    }

    public async Task<Result<Organization>> GetDefaultOrganization()
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDefaultOrganization);

        if (org is null)
        {
            return Result.Fail<Organization>("Organization not found.");
        }

        return Result.Ok(org);
    }

    public async Task<Result> UpdateOrganizationName(string orgId, string newName)
    {
        using var dbContext = _appDbFactory.GetContext();

        var org = await dbContext.Organizations.FirstOrDefaultAsync(x => x.ID == orgId);

        if (org is null)
        {
            return Result.Fail("Organization not found.");
        }

        org.OrganizationName = newName;
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task SetIsDefaultOrganization(string orgID, bool isDefault)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations.FindAsync(orgID);
        if (organization is null)
        {
            return;
        }

        if (isDefault)
        {
            await dbContext.Organizations.ForEachAsync(x => x.IsDefaultOrganization = false);
        }

        organization.IsDefaultOrganization = isDefault;
        await dbContext.SaveChangesAsync();
    }

    public int GetOrganizationCount()
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.Organizations.Count();
    }

    public async Task<int> GetOrganizationCountAsync()
    {
        using var dbContext = _appDbFactory.GetContext();

        return await dbContext.Organizations.CountAsync();
    }

    public async Task<Result<BrandingInfo>> GetBrandingInfo(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Result.Fail<BrandingInfo>("Organization ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
          .AsNoTracking()
          .Include(x => x.BrandingInfo)
          .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization is null)
        {
            return Result.Fail<BrandingInfo>("Organization not found.");
        }

        if (organization.BrandingInfo is null)
        {
            // The branding row is created on the first read, the same way the
            // settings row is seeded.
            var brandingInfo = new BrandingInfo()
            {
                OrganizationId = organizationId
            };

            dbContext.BrandingInfos.Add(brandingInfo);
            organization.BrandingInfo = brandingInfo;

            await dbContext.SaveChangesAsync();
        }
        return Result.Ok(organization.BrandingInfo);
    }

    public async Task UpdateBrandingInfo(
        string organizationId,
        string productName,
        byte[] iconBytes)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
            .Include(x => x.BrandingInfo)
            .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization is null)
        {
            return;
        }

        organization.BrandingInfo ??= new BrandingInfo();

        organization.BrandingInfo.Product = productName;

        if (iconBytes?.Any() == true)
        {
            organization.BrandingInfo.Icon = iconBytes;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task ResetBranding(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = await dbContext.Organizations
           .Include(x => x.BrandingInfo)
           .FirstOrDefaultAsync(x => x.ID == organizationId);

        if (organization?.BrandingInfo is null)
        {
            return;
        }

        // Only the values are copied.  Copying the whole default object also
        // copies its empty key and its empty organization id, and EF refuses
        // to change the key of an entity that is already tracked.
        organization.BrandingInfo.Product = BrandingInfo.Default.Product;
        organization.BrandingInfo.Icon = BrandingInfo.Default.Icon;

        await dbContext.SaveChangesAsync();
    }
}
