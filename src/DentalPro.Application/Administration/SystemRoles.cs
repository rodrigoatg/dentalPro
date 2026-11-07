namespace DentalPro.Application.Administration;

public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string Dentist = "Dentist";
    public const string Receptionist = "Receptionist";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Administrator, Dentist, Receptionist]);
}
