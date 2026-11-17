# 3. Arquitectura y dependencias

## Monolito modular

Se despliega una API y PostgreSQL compartidos, con límites internos por responsabilidad. No hay una razón técnica observada para desplegar cada módulo como microservicio; eso añadiría red, despliegues múltiples y consistencia distribuida sin necesidad actual. Las fronteras de proyectos permiten mantener cohesión sin ese costo.

```mermaid
flowchart BT
  Domain[DentalPro.Domain]
  Application[DentalPro.Application]
  Infrastructure[DentalPro.Infrastructure]
  Api[DentalPro.Api]
  Application --> Domain
  Infrastructure --> Application
  Infrastructure --> Domain
  Api --> Application
  Api --> Infrastructure
```

`Program.cs` compone dependencias. Application conoce abstracciones como `IAppointmentRepository`, no `DbContext`. Infrastructure implementa esas abstracciones. Domain no referencia ASP.NET, EF Core, Identity ni Npgsql.

## Capas

### Domain

Contiene conceptos e invariantes independientes del transporte. `Patient.Create` exige datos identificatorios básicos y rechaza nacimiento futuro; `Appointment` controla transición; `ClinicSchedule` valida configuración/intervalos. Fábricas y mutadores públicos mantienen estado válido; setters privados limitan modificaciones externas.

### Application

Coordina intenciones. `CreateAppointment` valida permiso y alcance, participantes, horario, disponibilidad, guarda y pide auditoría vía interfaces. `CreatePatient` delega creación al Domain y almacenamiento a `IPatientRepository`. Application no conoce códigos HTTP.

### Infrastructure

Implementa EF, PostgreSQL, Identity, repositorios, consultas de permiso, locks y auditoría. `DentalProDbContext` deriva de `IdentityDbContext`; configuraciones EF declaran tipos, claves e índices. SQL/locks/especificidades PostgreSQL permanecen aquí.

### API

`Program.cs` registra host/servicios y mapea Minimal API. Handlers enlazan query/route/body, extraen identidad, llaman Application y construyen DTO HTTP. Requests/responses no son entidades Domain ni EF.

### Angular

Presenta flujos, llama rutas relativas `/api` y muestra controles según permisos. El servidor Angular proxy retransmite solicitudes durante desarrollo. La API siempre vuelve a autenticar y autorizar.

## Ciclo de una solicitud

```mermaid
sequenceDiagram
  actor U as Usuario
  participant C as Componente Angular
  participant S as Servicio Angular
  participant I as Interceptor
  participant E as Endpoint API
  participant A as Application
  participant D as Domain
  participant R as Infrastructure
  participant DB as PostgreSQL
  U->>C: acción
  C->>S: comando/consulta
  S->>I: HttpClient /api
  I->>E: HTTP con Bearer
  E->>E: auth + permiso endpoint
  E->>A: datos + ActorId
  A->>D: regla/agregado
  A->>R: interfaces
  R->>DB: EF/SQL
  DB-->>R: resultados/commit
  R-->>A: resultado
  A-->>E: contrato Application
  E-->>S: DTO HTTP
  S-->>C: Observable
  C-->>U: vista actualizada
```

## Transacciones por flujo

Agenda y ClinicalRecords comparten `AppointmentUnitOfWork`: inicia transacción, ejecuta callback, guarda y confirma; si falla revierte y limpia ChangeTracker. Las operaciones de pacientes usan `PatientRepository` y no el UoW compartido. No supongas una transacción universal.

Reserva bloquea configuración con `FOR SHARE`; cambio de horario usa `FOR UPDATE`, ambos dentro del UoW. Las exclusion constraints PostgreSQL impiden solapamientos aun con escrituras concurrentes. La comprobación de disponibilidad aislada no bastaría frente a dos solicitudes simultáneas.
