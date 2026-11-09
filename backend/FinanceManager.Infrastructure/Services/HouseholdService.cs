using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using FinanceManager.Application.DTOs.Households;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Services;

public sealed class HouseholdService : IHouseholdService
{
    private const int MaximumMembers = 20;
    private static readonly TimeSpan InvitationLifetime =
        TimeSpan.FromDays(7);

    private readonly FinanceDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly IHouseholdAuthorizationService _authorization;

    public HouseholdService(
        FinanceDbContext db,
        UserManager<ApplicationUser> users,
        IEmailService email,
        IConfiguration configuration,
        IHouseholdAuthorizationService authorization)
    {
        _db = db;
        _users = users;
        _email = email;
        _configuration = configuration;
        _authorization = authorization;
    }

    public async Task<List<HouseholdResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _authorization.GetHouseholdIdsAsync(
            userId,
            ct);
        var households = await _db.Households
            .AsNoTracking()
            .Where(x => householdIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var householdMembers = await _db.HouseholdMembers
            .AsNoTracking()
            .Where(x => householdIds.Contains(x.HouseholdId))
            .ToListAsync(ct);
        var memberIds = householdMembers.Select(x => x.UserId).Distinct();
        var userDetails = await _users.Users
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FirstName,
                x.LastName
            })
            .ToDictionaryAsync(x => x.Id, ct);

        return households.Select(household =>
        {
            var members = householdMembers
                .Where(x => x.HouseholdId == household.Id)
                .Select(x =>
                {
                    if (!userDetails.TryGetValue(x.UserId, out var user))
                        throw new InvalidOperationException(
                            "A household member account could not be found.");

                    return new HouseholdMemberResponse(
                        user.Id,
                        user.Email ?? string.Empty,
                        user.FirstName,
                        user.LastName,
                        x.Role,
                        x.JoinedAtUtc);
                })
                .OrderBy(x => x.Role)
                .ThenBy(x => x.Email)
                .ToList();

            var role = householdMembers.Single(
                x => x.HouseholdId == household.Id &&
                     x.UserId == userId).Role;
            return new HouseholdResponse(
                household.Id,
                household.Name,
                household.OwnerUserId,
                household.CreatedAtUtc,
                role is HouseholdRole.Owner or HouseholdRole.Admin,
                role is HouseholdRole.Owner or
                    HouseholdRole.Admin or
                    HouseholdRole.Editor,
                role == HouseholdRole.Owner,
                members);
        }).ToList();
    }

    public async Task<HouseholdResponse> CreateAsync(
        Guid userId,
        CreateHouseholdRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Household name is required and cannot exceed 100 characters.");
        }

        var now = DateTime.UtcNow;
        var household = new Household
        {
            OwnerUserId = userId,
            Name = request.Name.Trim(),
            CreatedAtUtc = now
        };
        _db.Households.Add(household);
        _db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Owner,
            JoinedAtUtc = now
        });
        await _db.SaveChangesAsync(ct);

        return (await GetAllAsync(userId, ct))
            .Single(x => x.Id == household.Id);
    }

    public async Task<HouseholdInvitationResponse> InviteAsync(
        Guid userId,
        Guid householdId,
        InviteHouseholdMemberRequest request,
        CancellationToken ct = default)
    {
        var actorRole = await _authorization.GetRoleAsync(
            userId,
            householdId,
            ct);
        if (actorRole is not (HouseholdRole.Owner or HouseholdRole.Admin))
            throw new UnauthorizedAccessException(
                "Only household owners and admins can invite members.");

        var email = NormalizeEmail(request.Email);
        var invitedRole = ParseInvitedRole(request.Role);
        if (invitedRole == HouseholdRole.Admin &&
            actorRole != HouseholdRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the household owner can invite an admin.");
        }

        var household = await _db.Households
            .SingleOrDefaultAsync(x => x.Id == householdId, ct);
        if (household is null)
            throw new ArgumentException("Household not found.");

        var memberCount = await _db.HouseholdMembers
            .CountAsync(x => x.HouseholdId == householdId, ct);
        if (memberCount >= MaximumMembers)
            throw new ArgumentException(
                $"A household cannot have more than {MaximumMembers} members.");

        var existingUser = await _users.FindByEmailAsync(email);
        if (existingUser is not null &&
            await _authorization.IsMemberAsync(
                existingUser.Id,
                householdId,
                ct))
        {
            throw new ArgumentException(
                "This person is already a household member.");
        }

        var now = DateTime.UtcNow;
        var token = CreateToken();
        var tokenHash = HashToken(token);
        var normalizedEmail = email.ToUpperInvariant();
        var invitation = await _db.HouseholdInvitations
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId &&
                     x.NormalizedEmail == normalizedEmail &&
                     x.AcceptedAtUtc == null &&
                     x.ExpiresAtUtc > now,
                ct);

        if (invitation is null)
        {
            invitation = new HouseholdInvitation
            {
                HouseholdId = householdId,
                InvitedByUserId = userId,
                Email = email,
                NormalizedEmail = normalizedEmail,
                Role = invitedRole,
                TokenHash = tokenHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(InvitationLifetime)
            };
            _db.HouseholdInvitations.Add(invitation);
        }
        else
        {
            invitation.InvitedByUserId = userId;
            invitation.Email = email;
            invitation.Role = invitedRole;
            invitation.TokenHash = tokenHash;
            invitation.CreatedAtUtc = now;
            invitation.ExpiresAtUtc = now.Add(InvitationLifetime);
        }

        await _db.SaveChangesAsync(ct);

        var frontendUrl = _configuration["Frontend:BaseUrl"];
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps &&
             baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl must be configured as an absolute HTTP or HTTPS URL.");
        }

        var invitationUrl = new Uri(
            baseUri,
            $"households/accept?token={Uri.EscapeDataString(token)}")
            .ToString();
        await _email.SendHouseholdInvitationAsync(
            email,
            household.Name,
            invitationUrl,
            ct);

        return new HouseholdInvitationResponse(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAtUtc);
    }

    public async Task<bool> AcceptInvitationAsync(
        Guid userId,
        AcceptHouseholdInvitationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return false;

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed ||
            string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var tokenHash = HashToken(request.Token.Trim());
        var now = DateTime.UtcNow;
        var invitation = await _db.HouseholdInvitations
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.NormalizedEmail == user.Email.ToUpperInvariant() &&
                     x.AcceptedAtUtc == null &&
                     x.ExpiresAtUtc > now,
                ct);
        if (invitation is null)
            return false;

        if (await _authorization.IsMemberAsync(
                userId,
                invitation.HouseholdId,
                ct))
        {
            return false;
        }

        var memberCount = await _db.HouseholdMembers
            .CountAsync(x => x.HouseholdId == invitation.HouseholdId, ct);
        if (memberCount >= MaximumMembers)
            return false;

        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);
        invitation.AcceptedAtUtc = now;
        _db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = invitation.HouseholdId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAtUtc = now
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(
        Guid userId,
        Guid householdId,
        Guid memberUserId,
        CancellationToken ct = default)
    {
        var actorRole = await _authorization.GetRoleAsync(
            userId,
            householdId,
            ct);
        if (actorRole is not (HouseholdRole.Owner or HouseholdRole.Admin))
            throw new UnauthorizedAccessException(
                "Only household owners and admins can remove members.");

        var member = await _db.HouseholdMembers
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId &&
                     x.UserId == memberUserId,
                ct);
        if (member is null || member.Role == HouseholdRole.Owner)
            return false;

        if (actorRole == HouseholdRole.Admin &&
            member.Role == HouseholdRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Admins cannot remove another admin.");
        }

        _db.HouseholdMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateMemberRoleAsync(
        Guid userId,
        Guid householdId,
        Guid memberUserId,
        UpdateHouseholdMemberRoleRequest request,
        CancellationToken ct = default)
    {
        var actorRole = await _authorization.GetRoleAsync(
            userId,
            householdId,
            ct);
        if (actorRole is not (HouseholdRole.Owner or HouseholdRole.Admin))
            throw new UnauthorizedAccessException(
                "Only household owners and admins can change member roles.");

        var newRole = ParseMemberRole(request.Role);
        if (actorRole == HouseholdRole.Admin &&
            newRole == HouseholdRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Only the household owner can assign the admin role.");
        }

        var member = await _db.HouseholdMembers
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId &&
                     x.UserId == memberUserId,
                ct);
        if (member is null || member.Role == HouseholdRole.Owner)
            return false;

        member.Role = newRole;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static HouseholdRole ParseInvitedRole(string? value)
    {
        if (string.Equals(value, "viewer", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Viewer;
        if (string.Equals(value, "editor", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Editor;
        if (string.Equals(value, "admin", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Admin;

        throw new ArgumentException(
            "Invitation role must be Admin, Editor, or Viewer.");
    }

    private static HouseholdRole ParseMemberRole(string? value)
    {
        if (string.Equals(value, "viewer", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Viewer;
        if (string.Equals(value, "editor", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Editor;
        if (string.Equals(value, "admin", StringComparison.OrdinalIgnoreCase))
            return HouseholdRole.Admin;

        throw new ArgumentException(
            "Member role must be Admin, Editor, or Viewer.");
    }

    private static string NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 256)
            throw new ArgumentException("Enter a valid email address.");

        try
        {
            var address = new MailAddress(value.Trim());
            if (!string.Equals(
                    address.Address,
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Enter a valid email address.");
            }

            return address.Address;
        }
        catch (FormatException)
        {
            throw new ArgumentException("Enter a valid email address.");
        }
    }

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
