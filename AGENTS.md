# DentalPro - Agent Instructions

## 1. Project Overview

DentalPro is a web application for managing the operations of a dental clinic.

The project follows a modular monolith architecture and is designed to evolve over time without unnecessary coupling between business logic, infrastructure and presentation.

The authoritative project documentation is located under `docs/`.

---

## 2. Source of Truth

Before making architectural or domain-related changes, consult the relevant documentation:

- `docs/product/requirements.md`
- `docs/project-context.md`
- `docs/domain/domain-model.md`
- `docs/architecture/architecture.md`
- `docs/regulatory-requirements.md`
- `docs/architecture/decisions/`

Do not contradict documented domain rules or architectural decisions.

If a requested change conflicts with the documentation, explain the conflict before implementing it.

Do not silently redefine existing domain concepts.

---

## 3. MVP Scope

The current MVP includes:

- Authentication
- Authorization
- Role-based access control
- Users and roles
- Patients
- Appointments and agenda
- Digital medical records
- Clinical entries
- Clinical entry versioning
- Treatments
- Partial treatments
- Simplified odontogram
- Relevant audit events

The following capabilities are outside the current MVP and must not be implemented unless explicitly requested:

- Two-factor authentication
- Medical image storage
- Financial management
- Advanced financial reporting
- Notifications
- Clinical record export
- Consent management
- External healthcare integrations
- FHIR
- SNOMED CT
- Patient portal

Do not implement roadmap features simply because the domain model contains their concepts.

---

## 4. Architecture

DentalPro uses a modular monolith architecture.

The solution is divided into:

- `DentalPro.Domain`
- `DentalPro.Application`
- `DentalPro.Infrastructure`
- `DentalPro.Api`

The dependency direction must remain consistent with the architecture documentation.

### Domain

The Domain layer contains business concepts and business rules.

It must not depend on:

- Entity Framework Core
- ASP.NET Core
- HTTP
- database providers
- external services
- UI frameworks

### Application

The Application layer contains use cases and application orchestration.

It may depend on the Domain layer.

Business rules that belong to the domain should not be moved into Application merely for convenience.

### Infrastructure

Infrastructure contains technical implementations such as:

- persistence
- Entity Framework Core
- database providers
- external service integrations
- technical implementations of application abstractions

Infrastructure may depend on Application and Domain.

### API

The API layer exposes application functionality through HTTP.

Controllers/endpoints should remain thin.

Business rules must not be implemented directly inside controllers.

## Technology Stack

The current technology stack is:

- .NET 10 LTS
- ASP.NET Core 10
- C# 14
- Entity Framework Core 10
- PostgreSQL 18
- Npgsql
- xUnit

Do not downgrade the project to .NET 9 or another framework version without an explicit architectural decision.

PostgreSQL is the initial database provider.

Do not introduce PostgreSQL-specific behavior into Domain or Application unless there is a documented reason.

Database-provider-specific behavior belongs in Infrastructure.

---

## 5. Domain Rules

Do not create, remove, rename or substantially change domain entities without first updating the domain documentation and, when appropriate, creating an ADR.

Established domain terminology must be preserved.

Examples include:

- Patient
- Appointment
- MedicalRecord
- ClinicalEntry
- ClinicalEntryVersion
- Treatment
- TreatmentTooth
- Odontogram
- OdontogramTooth
- ToothStateHistory
- User
- Role
- Permission
- AuditEvent

Do not introduce alternative names for existing concepts without justification.

---

## 6. Clinical Data

Clinical information must be treated as traceable information.

Clinical entries must not be silently overwritten or deleted.

When a clinical entry is modified, preserve the previous version according to the rules defined in the domain documentation.

Treatment history must remain historically accurate.

A partially completed treatment must not be rewritten to represent work that was not actually completed.

The odontogram represents the patient's current dental state.

Historical changes must remain traceable where required by the domain model.

---

## 7. Persistence

Persistence concerns must remain outside the Domain layer.

Use abstractions where appropriate so that business logic is not tightly coupled to a specific database provider.

Do not introduce database-specific logic into Domain.

Do not create migrations until the persistence model has been reviewed.

Do not expose persistence entities directly as API contracts.

---

## 8. API

Use explicit request and response contracts.

Do not expose Domain entities directly from API endpoints.

Validate input at the application/API boundary.

Keep controllers or endpoint handlers focused on HTTP concerns.

Do not place business logic inside controllers.

---

## 9. Security

Authentication and authorization are cross-cutting concerns.

Respect the role and permission model documented in the project.

Never hard-code authorization decisions inside business entities.

Do not store plaintext passwords.

Secrets, credentials, tokens and connection strings must not be committed to the repository.

Development credentials must not be used as production credentials.

---

## 10. Testing

Every meaningful business rule should have automated tests.

Prefer unit tests for:

- domain rules
- value objects
- application use cases

Use integration tests for:

- persistence
- database interaction
- API integration
- infrastructure behavior

A task is not considered complete if the solution does not build successfully or existing tests fail.

Do not remove or weaken existing tests simply to make a task pass.

---

## 11. Database Providers

The initial implementation uses PostgreSQL unless the architecture documentation states otherwise.

Database-specific code must remain isolated in Infrastructure.

Do not assume that application or domain logic can directly depend on PostgreSQL-specific behavior.

Future support for other relational database providers should remain possible without redesigning the Domain layer.

---

## 12. Changes and Refactoring

Prefer small, focused changes.

Do not perform unrelated refactoring while implementing a feature.

Do not introduce abstractions without a concrete need.

Avoid premature generalization.

Prefer simple solutions that preserve the architecture and allow future evolution.

If a requested change requires an architectural decision, stop and propose the decision before implementing it.

---

## 13. Documentation

When an implementation changes an established architectural or domain decision:

1. Identify the affected documentation.
2. Update the relevant document.
3. Create or update an ADR when appropriate.

Do not leave the implementation and documentation describing different systems.

---

## 14. Agent Workflow

For non-trivial tasks:

1. Inspect the repository and relevant documentation.
2. Explain the intended approach.
3. Identify assumptions or conflicts.
4. Implement only the requested scope.
5. Run formatting, build and relevant tests.
6. Review the resulting diff.
7. Report the files changed and validation results.

Do not make broad changes without explicit justification.

Do not implement future roadmap functionality unless explicitly requested.

---

## 15. Definition of Done

A task is considered complete only when:

- The requested functionality is implemented.
- The architecture remains consistent.
- Existing tests pass.
- New business rules have appropriate tests.
- The solution builds without errors.
- No unrelated files were modified.
- Documentation is updated when required.
- The final diff has been reviewed.