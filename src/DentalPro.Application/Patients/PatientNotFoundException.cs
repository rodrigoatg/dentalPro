namespace DentalPro.Application.Patients;

public sealed class PatientNotFoundException : Exception
{
    public PatientNotFoundException() : base("Patient was not found.") { }
}
