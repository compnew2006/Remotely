using Microsoft.EntityFrameworkCore;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using Remotely.Shared.ViewModels;

namespace Remotely.Server.Services;

/// <summary>
/// The invitation links that let a user join an organization.
/// </summary>
public interface IInviteService
{
    Task<Result<InviteLink>> AddInvite(string orgId, InviteViewModel invite);

    Task<Result> DeleteInvite(string orgId, string inviteId);

    InviteLink[] GetAllInviteLinks(string organizationId);

    Task<Result> JoinViaInvitation(string userName, string inviteId);
}

public class InviteService : IInviteService
{
    private readonly IAppDbFactory _appDbFactory;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        IAppDbFactory appDbFactory,
        ILogger<InviteService> logger)
    {
        _appDbFactory = appDbFactory;
        _logger = logger;
    }

    public async Task<Result<InviteLink>> AddInvite(string orgId, InviteViewModel invite)
    {
        using var dbContext = _appDbFactory.GetContext();

        var organization = dbContext.Organizations
            .Include(x => x.InviteLinks)
            .FirstOrDefault(x => x.ID == orgId);

        if (organization is null)
        {
            return Result.Fail<InviteLink>("Organization not found.");
        }

        var inviteLink = new InviteLink()
        {
            InvitedUser = invite.InvitedUser?.ToLower(),
            DateSent = DateTimeOffset.Now,
            IsAdmin = invite.IsAdmin,
            Organization = organization,
            OrganizationID = organization.ID,
        };

        organization.InviteLinks ??= new List<InviteLink>();
        organization.InviteLinks.Add(inviteLink);
        await dbContext.SaveChangesAsync();
        return Result.Ok(inviteLink);
    }

    public async Task<Result> DeleteInvite(string orgId, string inviteID)
    {
        using var dbContext = _appDbFactory.GetContext();

        var invite = dbContext.InviteLinks.FirstOrDefault(x =>
            x.OrganizationID == orgId &&
            x.ID == inviteID);

        if (invite is null)
        {
            return Result.Fail("Invite not found.");
        }

        var user = dbContext.Users.FirstOrDefault(x => x.UserName == invite.InvitedUser);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            dbContext.Remove(user);
        }

        dbContext.Remove(invite);
        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> JoinViaInvitation(string userName, string inviteId)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail("Username cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(inviteId))
        {
            return Result.Fail("Invite ID cannot be empty.");
        }

        using var dbContext = _appDbFactory.GetContext();

        var invite = await dbContext.InviteLinks
            .FirstOrDefaultAsync(x =>
                x.InvitedUser!.ToLower() == userName.ToLower() &&
                x.ID == inviteId);

        if (invite is null)
        {
            return Result.Fail("Invite not found.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var organization = await dbContext.Organizations
            .Include(x => x.RemotelyUsers)
            .FirstOrDefaultAsync(x => x.ID == invite.OrganizationID);

        if (organization is null)
        {
            return Result.Fail("Organization not found.");
        }

        user.Organization = organization;
        user.OrganizationID = organization.ID;
        user.IsAdministrator = invite.IsAdmin;
        organization.RemotelyUsers.Add(user);

        await dbContext.SaveChangesAsync();

        dbContext.InviteLinks.Remove(invite);
        dbContext.SaveChanges();
        return Result.Ok();
    }

    public InviteLink[] GetAllInviteLinks(string organizationId)
    {
        using var dbContext = _appDbFactory.GetContext();

        return dbContext.InviteLinks
            .AsNoTracking()
            .Where(x => x.OrganizationID == organizationId)
            .ToArray();
    }
}
