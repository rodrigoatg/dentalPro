# DentalPro — Architecture

## 1. Architecture Overview

DentalPro is a web application based on a client-server architecture.

The system is organized into three primary layers:

1. Presentation
2. Application / Business Logic
3. Data

The implementation will use a modular monolith approach,
with internal module boundaries that allow future evolution.

## 2. Technology Stack

### Frontend

Angular

Responsibilities:

- User interface.
- Navigation.
- Forms.
- Client-side validation.
- Role-aware presentation.
- Communication with the backend API.

### Backend

ASP.NET Core Web API

Responsibilities:

- Business logic.
- API endpoints.
- Authentication.
- Authorization.
- Validation.
- Persistence coordination.

### Database

The application will use a relational database accessed through
Entity Framework Core.

PostgreSQL will be the primary database engine for the initial
deployment.

The persistence architecture should minimize vendor-specific
database dependencies so that SQL Server or MySQL can be adopted
with limited implementation changes when required by a client.

Database-specific functionality must be isolated from the domain
and application layers.

### Database Provider Strategy

The application will support a configurable database provider.

Initial provider:

- PostgreSQL

Potential supported providers:

- Microsoft SQL Server
- MySQL

The application should avoid database-specific SQL and provider-specific
features unless there is a documented technical justification.

Provider-specific configuration and implementation details must remain
within the Infrastructure layer.

### File Storage

Future scope: when medical images and clinical file storage are introduced,
large files should use object/cloud storage rather than the relational
database. File storage for clinical images is not part of the MVP.

## 3. Architectural Style

The backend will follow a Modular Monolith architecture.

The application will initially be deployed as a single
backend application, while maintaining clear boundaries
between business modules.

Initial modules:

- Identity
- Patients
- Scheduling
- Clinical

Future modules:

- Finance
- Reporting
- Notifications
- MedicalImages
- Export

## 4. Internal Architecture

Each business module should follow separation between:

- Domain
- Application
- Infrastructure
- API

Example:

Patients
├── Domain
├── Application
├── Infrastructure
└── API

## 5. Dependency Rules

Domain must not depend on Infrastructure.

Application may depend on Domain.

Infrastructure implements interfaces defined by
the Domain or Application layers.

API communicates with the Application layer.

Controllers must not contain business logic.

## 6. Communication

Frontend and backend communicate through HTTPS
using REST and JSON.

Example:

Angular
    |
    | HTTPS / REST
    v
ASP.NET Core API
    |
    v
Application
    |
    v
Domain
    |
    v
Infrastructure
    |
    v
PostgreSQL

## 7. Authentication and Authorization

Authentication will use individual user credentials managed by ASP.NET Core
Identity and JWT Bearer access tokens.

Authorization will use configurable role-permission assignments. The initial
roles are Administrator, Dentist and Receptionist. A user may have multiple
roles; effective permissions are the union of permissions assigned to all
current roles. Permissions are assigned only to roles, never directly to
users.

The permission catalog has one canonical definition in backend code. Stable
permission codes identify capabilities; names and descriptions are mutable
metadata. Only permissions corresponding to implemented API endpoints belong
in the catalog. The initial catalog and role matrix are documented in
`docs/domain/domain-model.md`.

The backend will be responsible for enforcing authorization.

The frontend may hide functionality based on the user's
permissions, but this must never be considered the primary
security mechanism.

The API requires authentication by default and permits anonymous access only
for explicitly public endpoints such as login. It evaluates current
role-permission assignments on each authorization decision; permission
claims in a JWT are not authoritative. Permission changes apply to the next
request, without a new sign-in. The MVP does not cache effective permissions.

Deactivation or role changes invalidate earlier JWTs through per-request
validation of the user's current active state and security stamp (or an
equivalent security version). Validation errors fail closed. A change to
role-permission assignments affects authorization immediately and does not
depend on a token refresh.

Protected administrative permissions are defined in backend code and cannot
be removed from Administrator through normal API operations. The last active
Administrator cannot be demoted or deactivated, including through concurrent
administrative operations. The first Administrator is created through a
secure, idempotent operational bootstrap, not a public endpoint.

The MVP includes individual credentials, password hashing, backend
authorization, RBAC, HTTPS and session/token expiration. Angular keeps the
access token in memory only; it does not use localStorage, sessionStorage or
refresh tokens. Two-factor authentication (2FA) is future scope and is not
required for the MVP.

### RBAC responsibilities by layer

- Domain owns the technology-independent Permission and RolePermission
  concepts and their local invariants. It does not reference ASP.NET Core
  Identity or Entity Framework Core. RoleId is an opaque Guid compatible
  with the future Identity role key.
- Application owns the code-defined permission catalog contract, effective
  permission query contract, and role-permission assignment repository
  contract. Application use cases enforce protected-permission and
  last-Administrator rules when those account-management operations are
  implemented.
- Infrastructure will implement catalog seeding and persistence, resolve
  Identity user/role membership, and query current effective permissions.
- API will declare permission requirements on endpoints and delegate checks
  to Application services. It will not contain permission assignment or
  business-rule logic.

## 8. Security

The architecture must support:

- HTTPS.
- Password hashing.
- JWT-based authentication.
- RBAC with configurable permissions per role.
- Immediate permission change effect and token revocation on user state or
  role changes.
- Audit logging.
- Session/token expiration.
- Future 2FA capability for unrecognized devices.

## 9. Persistence

Entity Framework Core will provide access to PostgreSQL.

Database access will be isolated inside Infrastructure.

Business rules must not depend directly on Entity Framework
or PostgreSQL-specific implementation details.

## 10. Scalability

The modular structure allows new capabilities to be added
without restructuring the entire application.

Future modules may be implemented independently within
the monolith.

If a module eventually requires independent scaling or
deployment, its internal boundaries should make extraction
into a separate service possible.

Microservices are not part of the initial implementation.

## 11. Testing

The project should include:

- Unit tests.
- Integration tests.
- API tests where appropriate.

Business rules should be covered by automated tests.

## 12. Deployment

The development environment should be reproducible
using containerization.

Docker and Docker Compose may be used to provide:

- Application services.
- PostgreSQL.
- Supporting development services.

## 13. Observability

The backend should provide:

- Structured logging.
- Health checks.
- Error logging.
- Basic application diagnostics.

## 14. Architectural Principles

The implementation should prioritize:

- Separation of concerns.
- High cohesion.
- Low coupling.
- Explicit business rules.
- Testability.
- Security.
- Maintainability.
- Incremental scalability.

## 15. Clinical Information Architecture

Clinical information is treated as a protected and auditable domain.

The architecture separates clinical domain rules from infrastructure,
authentication, persistence and external integrations.

The system must support:

- Controlled access to clinical information.
- Clinical data integrity.
- Non-destructive updates.
- Auditability.
- Long-term data retention.
- Backup and recovery.
- Future interoperability.

For the MVP, clinical integrity includes preserved ClinicalEntry versions,
traceable treatment and Odontogram changes, and AuditEvent records for
relevant operations. This does not prescribe a persistence mechanism or
require auditing every read operation.

The Domain layer must not depend directly on:

- PostgreSQL.
- SQL Server.
- MySQL.
- FHIR.
- SNOMED CT.
- Cloud storage providers.
- Authentication providers.

Infrastructure and integration concerns must remain outside the Domain layer.

## 16. Future Interoperability

DentalPro should be designed so that future healthcare interoperability
capabilities can be introduced without modifying the core clinical domain.

Potential future capabilities include:

- FHIR-based APIs.
- Standardized clinical terminology.
- External health information exchange.
- Patient portals.
- Clinical document exchange.

These capabilities are outside the initial MVP.

## 17. Scheduling Module Boundary

The `Appointment` Domain type, scheduling Application use cases, appointment
and clinic-schedule EF Core persistence, and migrations are implemented. API
endpoints and Angular screens remain unimplemented. The implementation
preserves the modular dependency rules:

- Domain owns appointment invariants and state transitions and does not
  reference Identity, EF Core or PostgreSQL.
- Application orchestrates eligibility, availability, scope checks and
  atomic appointment operations through application-owned contracts.
- Infrastructure maps appointment persistence rows, validates Patient and
  Identity-backed Dentist state, stores clinic schedule configuration, and
  implements PostgreSQL-specific overlap constraints.
- The clinic schedule is a singleton aggregate with IANA time zone, duration
  default, start increment, weekly local-time intervals, and date exceptions.
  Local boundaries use `time without time zone`; appointment instants use UTC.
- Appointment effective duration is persisted separately and must match the
  UTC start/end difference. Its migration backfills existing rows without
  moving appointments and fails if a historic interval is not an exact whole
  number of minutes.
- Booking reads hold a shared row lock on the schedule singleton through the
  shared unit-of-work commit. Schedule changes acquire an exclusive row lock
  in that same unit of work, so concurrent configuration updates cannot alter
  the hours used by an in-flight booking.
- API exposes explicit contracts, requires the code-defined permission for
  each operation, and supplies the authenticated actor ID to Application.
- Angular may hide controls based on effective permissions but is never an
  authorization boundary.
- Appointment audit records are append-only and committed in the same
  transaction as the appointment operation. This is audit logging, not Event
  Sourcing.

The details of agenda scope, UTC instants, local clinic hours, PostgreSQL
concurrency enforcement and audit records are defined in ADR-004,
ADR-005 and ADR-006. Database migrations remain an explicit reviewed
deployment operation; API startup must not apply them automatically.
