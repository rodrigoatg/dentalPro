namespace DentalPro.IntegrationTests;

[CollectionDefinition("Appointment PostgreSQL", DisableParallelization = true)]
public sealed class PostgreSqlAppointmentCollection { }

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlAppointmentFactAttribute : FactAttribute
{
    public PostgreSqlAppointmentFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                PostgreSqlAppointmentTests.ConnectionStringEnvironmentVariable)))
        {
            Skip = $"Set {PostgreSqlAppointmentTests.ConnectionStringEnvironmentVariable} to an isolated PostgreSQL 17 test database.";
        }
    }
}
