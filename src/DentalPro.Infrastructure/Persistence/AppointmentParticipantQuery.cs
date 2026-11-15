using DentalPro.Application.Administration;
using DentalPro.Application.Appointments;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Persistence;

public sealed class AppointmentParticipantQuery(DentalProDbContext dbContext) : IAppointmentParticipantQuery
{
    public async Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetPatientNamesAsync(
        IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default) =>
        await dbContext.Patients.AsNoTracking()
            .Where(row => patientIds.Contains(row.Id))
            .Select(row => new { row.Id, row.FirstName, row.LastName })
            .ToDictionaryAsync(row => row.Id,
                row => new AppointmentParticipantName(row.FirstName, row.LastName), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetDentistNamesAsync(
        IReadOnlyCollection<Guid> dentistIds, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AsNoTracking()
            .Where(row => dentistIds.Contains(row.Id))
            .Select(row => new { row.Id, row.FirstName, row.LastName })
            .ToDictionaryAsync(row => row.Id,
                row => new AppointmentParticipantName(row.FirstName, row.LastName), cancellationToken);

    public async Task<IReadOnlyList<AppointmentDentistOption>> ListActiveDentistsAsync(
        CancellationToken cancellationToken = default) =>
        await (from user in dbContext.Users.AsNoTracking()
               join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
               join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               where user.IsActive && role.Name == SystemRoles.Dentist
               orderby user.LastName, user.FirstName, user.Id
               select new AppointmentDentistOption(user.Id, user.FirstName, user.LastName))
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<AppointmentPatientStatus> GetPatientStatusAsync(
        Guid patientId, CancellationToken cancellationToken = default)
    {
        var patient = await dbContext.Patients.AsNoTracking()
            .Where(row => row.Id == patientId)
            .Select(row => new { row.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        return patient is null
            ? AppointmentPatientStatus.NotFound
            : patient.IsActive ? AppointmentPatientStatus.Active : AppointmentPatientStatus.Inactive;
    }

    public async Task<AppointmentDentistStatus> GetDentistStatusAsync(
        Guid dentistId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(row => row.Id == dentistId)
            .Select(row => new { row.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return AppointmentDentistStatus.NotFound;
        if (!user.IsActive) return AppointmentDentistStatus.Inactive;

        var hasDentistRole = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == dentistId && role.Name == SystemRoles.Dentist
            select userRole.UserId)
            .AnyAsync(cancellationToken);

        return hasDentistRole
            ? AppointmentDentistStatus.ActiveDentist
            : AppointmentDentistStatus.NotDentist;
    }
}
