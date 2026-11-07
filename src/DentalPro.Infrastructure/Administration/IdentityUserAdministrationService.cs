using System.Data;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Administration;
using DentalPro.Application.Identity;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DentalPro.Infrastructure.Administration;

public sealed class IdentityUserAdministrationService(
    UserManager<ApplicationUser> userManager,
    DentalProDbContext dbContext,
    IEffectivePermissionQuery effectivePermissionQuery) : IUserAdministrationService
{
    public async Task<CurrentUserSummary?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        return new CurrentUserSummary(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray(),
            await effectivePermissionQuery.GetForUserAsync(userId, cancellationToken));
    }

    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users.AsNoTracking()
            .OrderBy(user => user.UserName)
            .ToListAsync(cancellationToken);
        var result = new List<UserSummary>(users.Count);
        foreach (var user in users)
        {
            result.Add(await ToSummaryAsync(user));
        }

        return result;
    }

    public async Task<UserSummary> CreateAsync(CreateUserInput input, CancellationToken cancellationToken = default)
    {
        ValidateRoles(input.Roles);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = input.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        EnsureIdentitySuccess(await userManager.CreateAsync(user, input.Password));
        EnsureIdentitySuccess(await userManager.AddToRolesAsync(user, input.Roles.Distinct(StringComparer.Ordinal)));
        await transaction.CommitAsync(cancellationToken);
        return await ToSummaryAsync(user);
    }

    public async Task<UserSummary> SetRolesAsync(
        Guid actorId,
        Guid targetUserId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        ValidateRoles(roleNames);
        using var administratorMutation = await LastAdministratorMutationGate.AcquireAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var user = await userManager.FindByIdAsync(targetUserId.ToString())
                ?? throw new AdministrationNotFoundException("User was not found.");
            var currentRoles = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);
            var requestedRoles = roleNames.ToHashSet(StringComparer.Ordinal);
            if (actorId == targetUserId && currentRoles.Contains(SystemRoles.Administrator) &&
                !requestedRoles.Contains(SystemRoles.Administrator))
            {
                throw new AdministrationConflictException("An administrator cannot remove their own Administrator role.");
            }

            if (user.IsActive && currentRoles.Contains(SystemRoles.Administrator) &&
                !requestedRoles.Contains(SystemRoles.Administrator) &&
                await CountActiveAdministratorsAsync(cancellationToken) <= 1)
            {
                throw new AdministrationConflictException("The last active Administrator cannot lose that role.");
            }

            var remove = currentRoles.Except(requestedRoles, StringComparer.Ordinal).ToArray();
            var add = requestedRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();
            if (remove.Length > 0)
            {
                EnsureIdentitySuccess(await userManager.RemoveFromRolesAsync(user, remove));
            }

            if (add.Length > 0)
            {
                EnsureIdentitySuccess(await userManager.AddToRolesAsync(user, add));
            }

            if (remove.Length > 0 || add.Length > 0)
            {
                EnsureIdentitySuccess(await userManager.UpdateSecurityStampAsync(user));
                user.UpdatedAt = DateTimeOffset.UtcNow;
                EnsureIdentitySuccess(await userManager.UpdateAsync(user));
            }

            await transaction.CommitAsync(cancellationToken);
            return await ToSummaryAsync(user);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw new AdministrationConflictException(
                "The administrator assignment changed concurrently. Retry the operation.");
        }
    }

    public async Task<UserSummary> SetStatusAsync(
        Guid actorId,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        using var administratorMutation = await LastAdministratorMutationGate.AcquireAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var user = await userManager.FindByIdAsync(targetUserId.ToString())
                ?? throw new AdministrationNotFoundException("User was not found.");
            if (user.IsActive == isActive)
            {
                await transaction.CommitAsync(cancellationToken);
                return await ToSummaryAsync(user);
            }

            var userRoles = await userManager.GetRolesAsync(user);
            if (!isActive && actorId == targetUserId &&
                userRoles.Contains(SystemRoles.Administrator, StringComparer.Ordinal))
            {
                throw new AdministrationConflictException("An Administrator cannot deactivate their own account.");
            }

            if (!isActive && userRoles.Contains(SystemRoles.Administrator, StringComparer.Ordinal) &&
                await CountActiveAdministratorsAsync(cancellationToken) <= 1)
            {
                throw new AdministrationConflictException("The last active Administrator cannot be deactivated.");
            }

            user.IsActive = isActive;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            // Rotating the stamp also prevents an old token becoming valid again after reactivation.
            EnsureIdentitySuccess(await userManager.UpdateSecurityStampAsync(user));
            await transaction.CommitAsync(cancellationToken);
            return await ToSummaryAsync(user);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw new AdministrationConflictException(
                "The administrator status changed concurrently. Retry the operation.");
        }
    }

    private async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        var administratorRoleId = await dbContext.Roles.AsNoTracking()
            .Where(role => role.Name == SystemRoles.Administrator)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (administratorRoleId is null)
        {
            return 0;
        }

        return await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => userRole.RoleId == administratorRoleId.Value)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                userRole => userRole.UserId,
                user => user.Id,
                (_, _) => 1)
            .CountAsync(cancellationToken);
    }

    private async Task<UserSummary> ToSummaryAsync(ApplicationUser user) => new(
        user.Id,
        user.UserName ?? string.Empty,
        user.Email,
        user.FirstName,
        user.LastName,
        user.IsActive,
        (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray(),
        user.CreatedAt);

    private static void ValidateRoles(IEnumerable<string>? roleNames)
    {
        if (roleNames is null || roleNames.Any(name => !SystemRoles.All.Contains(name, StringComparer.Ordinal)))
        {
            throw new AdministrationValidationException("One or more role names are not supported.");
        }
    }

    private static void EnsureIdentitySuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new AdministrationValidationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
}
