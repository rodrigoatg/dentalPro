namespace DentalPro.Application.Patients;

public sealed class PatientDocumentAlreadyExistsException()
    : Exception("A patient with the same document already exists.");
