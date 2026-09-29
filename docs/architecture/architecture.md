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

Clinical images and large documents should be stored
using object/cloud storage rather than directly inside
the relational database.

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

Authentication will use individual user credentials.

The system will implement role-based access control (RBAC).

The backend will be responsible for enforcing authorization.

The frontend may hide functionality based on the user's
permissions, but this must never be considered the primary
security mechanism.

## 8. Security

The architecture must support:

- HTTPS.
- Password hashing.
- JWT-based authentication.
- RBAC.
- Audit logging.
- Session/token expiration.
- 2FA for unrecognized devices.

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