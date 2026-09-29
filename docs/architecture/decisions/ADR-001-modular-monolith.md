# ADR-001 — Modular Monolith

## Status

Accepted

## Context

DentalPro requires several functional areas including
patients, appointments, clinical information, identity,
financial management and reporting.

The initial development team is small and the project
requires a functional implementation within the academic
course.

At the same time, the architecture should not prevent
future growth of the product.

## Decision

DentalPro will initially use a Modular Monolith architecture.

The system will be deployed as a single backend application,
but business capabilities will be separated into explicit
modules.

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

## Consequences

### Positive

- Simple deployment.
- Lower operational complexity.
- Easy local development.
- Clear separation of business responsibilities.
- Easier testing.
- Future possibility of extracting modules into independent
  services if required.

### Negative

- All modules initially share the same deployment.
- Module boundaries must be actively maintained.
- Some infrastructure is shared between modules.

## Alternatives considered

### Microservices

Rejected for the initial implementation because the additional
operational complexity is not justified by the current scale.

### Traditional layered monolith

Rejected because it does not provide sufficiently explicit
business module boundaries for future evolution.