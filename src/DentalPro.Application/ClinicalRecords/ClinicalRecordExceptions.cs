namespace DentalPro.Application.ClinicalRecords;

public abstract class ClinicalRecordApplicationException(string message) : Exception(message);
public sealed class ClinicalRecordNotFoundException() : ClinicalRecordApplicationException("Clinical record was not found.");
public sealed class ClinicalPatientUnavailableException() : ClinicalRecordApplicationException("Patient is inactive.");
public sealed class ClinicalRecordOutOfScopeException() : ClinicalRecordApplicationException("Clinical record access is outside the actor's scope.");
public sealed class ClinicalDentistUnavailableException() : ClinicalRecordApplicationException("The responsible dentist is unavailable.");
public sealed class ClinicalAppointmentMismatchException() : ClinicalRecordApplicationException("Appointment does not belong to this patient and dentist.");
public sealed class ClinicalTreatmentPermissionException() : ClinicalRecordApplicationException("Treatment creation is not permitted.");
