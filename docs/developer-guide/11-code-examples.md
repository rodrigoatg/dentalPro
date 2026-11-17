# 11. Ejemplos comentados

Los fragmentos son patrones de código actual abreviados; consultá el archivo enlazado para el contexto completo.

## Fábrica de Patient

En `src/DentalPro.Domain/Patients/Patient.cs`:

```csharp
ValidatePersonalInformation(documentType, documentNumber, firstName, lastName, dateOfBirth);
var patientId = Guid.NewGuid();
var now = DateTimeOffset.UtcNow;
var medicalRecord = MedicalRecord.Create(patientId, now);
return new Patient(patientId, documentType, documentNumber, firstName, lastName,
    dateOfBirth, phone, email, address, now, medicalRecord);
```

Valida antes de construir; identidad y hora se generan una vez y se comparten con el record. Constructor privado evita agregados incompletos.

## Application delega persistencia

`CreatePatient.ExecuteAsync` crea mediante `Patient.Create`, llama `patientRepository.AddAsync` y devuelve agregado. El caso de uso no conoce SQL/EF; Domain posee invariantes y contrato oculta almacenamiento.

## Reserva vuelve a validar dentro de UoW

Forma conceptual de `CreateAppointment.ExecuteAsync`:

```csharp
return unitOfWork.ExecuteAsync(async ct => {
    await scope.EnsurePermissionAsync(actorId, AppointmentPermissionCodes.Create, ct);
    await ValidatePatientAsync(command.PatientId, participants, ct);
    var settings = await clinicSchedule.GetSettingsAsync(ct);
    var start = ClinicScheduleTime.ResolveLocalStart(command.StartDateTime, settings);
    var duration = command.DurationMinutes ?? settings.DefaultAppointmentDurationMinutes;
    var end = ClinicScheduleTime.CalculateEnd(start, duration);
    await EnsureWithinClinicHoursAsync(clinicSchedule, start, end, ct);
    await EnsureAvailableAsync(repository, command.PatientId, command.DentistId,
        start, end, null, ct);
    repository.Add(Appointment.Create(command.PatientId, command.DentistId,
        start, duration, command.Notes));
    // Añade auditoría; UoW guarda y confirma al terminar callback.
}, cancellationToken);
```

Disponibilidad UI permite elegir; no reserva. Una petición concurrente puede llegar después, por eso escritura vuelve a validar y la base aplica exclusion constraints.

## Permiso de endpoint

`PatientEndpoints` protege `POST /api/patients` con `patients.create` y lectura con `patients.view`. El requirement se evalúa antes del handler. Este valida request y convierte excepción de documento duplicado a 409.

## Interceptor y servicio

`PatientsApiService` construye URL/query y devuelve Observable. `authInterceptor` adjunta Bearer a solicitudes autenticadas excepto login. Separar responsabilidades evita repetir manejo de token en cada servicio.

## Restricción por pieza dental

`TreatmentToothConfiguration` define PK compuesta `(TreatmentId, ToothNumber)`, checks FDI y estado, y consistencia de `CompletedAt`. Una Treatment puede tener varias filas; progreso no queda reducido al estado del agregado.

## Navegar el código

Desde `AppointmentsApiService.list`, buscar el llamador en `agenda.ts`; seguir el GET al endpoint, luego `ListAppointments`, contrato y `AppointmentRepository`. Para confirmar persistencia abre `AppointmentConfiguration` y su migration. Este recorrido vertical es más productivo que leer archivos en orden alfabético.
