# 4. Backend ASP.NET Core

## Organización

Todos los proyectos apuntan a `net10.0`:

- Domain: `Patients`, `Appointments`, `Treatments`, `AccessControl`.
- Application: `Patients`, `Appointments`, `ClinicalRecords`, `Administration`, `Identity`, `AccessControl`.
- Infrastructure: `Persistence`, `Persistence/Models`, `Persistence/Configurations`, `Migrations`, `Identity`, `Administration`.
- API: rutas agrupadas en `Patients`, `Appointments`, `ClinicalRecords`, `Administration`, `Identity`.

## Entidades y Value Objects

`Patient` es agregado que crea `MedicalRecord` y odontograma vacío. Usa Guid, `DateOnly` para nacimiento y `DateTimeOffset` para instantes. `Appointment` controla estado y transiciones. `ToothNumber` (`readonly record struct`) comprueba FDI mediante `Create(int)`. No todo valor está encapsulado en Value Object: algunos contratos reciben enums/strings.

## Casos de uso y contratos

Los use cases suelen ser clases `sealed` con dependencias inyectadas y método `ExecuteAsync`. `ListPatients` delega `SearchAsync`; `CreateAppointment` recibe actor, command y token de cancelación. Contratos como `AppointmentQuery`, `CreateAppointmentCommand`, `PatientDetails` son tipos Application independientes de HTTP.

## Repositorios y unidad de trabajo

`DentalProDbContext` hereda de Identity context y aplica `IEntityTypeConfiguration` del assembly. `PatientRepository` mapea agregado a filas y persiste Patient/MedicalRecord/Odontogram. `AppointmentRepository` usa `Appointment.Rehydrate`; `GetByIdForUpdateAsync` requiere transacción, mantiene agregado/row rastreados y evita identidades duplicadas. `UpdateAsync` copia solo estado y `UpdatedAt` del agregado original.

`AppointmentUnitOfWork.ExecuteAsync` abre transacción, ejecuta callback, `SaveChanges` y commit. Error provoca rollback y limpieza del tracker; el error PostgreSQL de exclusión se traduce a excepción Application de conflicto. El mismo UoW implementa unidad clínica, por lo que escritura clínica y auditoría participan en transacción compartida.

## Dependency Injection

`Program.cs` registra cada caso de uso y llama `AddInfrastructure(configuration)`. `InfrastructureServiceCollectionExtensions` configura Npgsql, Identity, opciones JWT, repositorios y UoW. Registrar un caso de uso en API y su implementación de contrato en Infrastructure según convención.

## Minimal APIs y DTO

`Program.cs` ejecuta middleware de autenticación/autorización y mapea endpoints. Ejemplos: Patient CRUD, rutas de Appointment, ClinicalRecord y Administration. Contratos de transporte viven en API (`AppointmentRequests`, `AppointmentResponses`, `PatientResponse`). No exponer `PatientRow` ni agregados EF.

## Validación y errores

API valida forma/campos enlazados; Domain protege invariantes del agregado; Application verifica permiso, participante, alcance y coordinación; PostgreSQL aplica constraints. Filtros por módulo traducen excepciones conocidas a Problem Details/status HTTP. Fallos inesperados no deben exponer internals.

Para seguir error: regla Domain → excepción Application → filtro API → respuesta HTTP → manejo Angular. Evitá corregir en la UI una inconsistencia de dominio.

## Actor, concurrencia y auditoría

Endpoints leen claim `sub` del JWT como Guid y pasan `ActorId`; nunca reciben actor desde JSON. Dentist ownership/scope se verifica también en Application. En agenda, disponibilidad se valida otra vez dentro de transacción; filas horarios se bloquean y constraints PostgreSQL protegen carrera. Auditoría usa `IAuditEventWriter`/`IClinicalAuditWriter`; es append-only, no Event Sourcing.

## Recorrido práctico

1. Abrí endpoint de módulo y sus DTO.
2. Sigue el método `ExecuteAsync` llamado.
3. Inspeccioná interfaces usadas.
4. Busca implementaciones en Infrastructure.
5. Revisa configuración EF y migración.
6. Busca el caso en `tests/`.
