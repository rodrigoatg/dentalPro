# ADR-002 — Preserve Clinical Record Integrity

## Status

Accepted

## Context

DentalPro manages clinical information that may be subject to legal and
regulatory requirements regarding integrity, authenticity, traceability,
confidentiality and preservation.

A simple CRUD approach that allows clinical records to be freely updated or
deleted would not provide sufficient traceability.

## Decision

DentalPro will use a non-destructive approach for relevant clinical
information.

Previously recorded clinical information must not be silently overwritten
or deleted.

Clinical modifications must preserve sufficient information to identify:

- The user performing the modification.
- The timestamp.
- The affected information.
- The operation performed.
- When applicable, the reason for the modification.

The exact persistence and versioning mechanism will be defined during the
implementation design phase.

For the MVP, every ClinicalEntry must be versioned so that previous
versions are preserved and accessible. This requirement does not select a
specific persistence mechanism. Full Event Sourcing is not required.

## Consequences

### Positive

- Better clinical traceability.
- Improved auditability.
- Reduced risk of accidental data loss.
- Better alignment with applicable regulatory requirements.
- A clear basis for future versioning of additional clinical documents.

### Negative

- More complex persistence logic than a traditional CRUD implementation.
- Additional storage requirements.
- Additional UI considerations when displaying historical information.
- More complex testing requirements.

## Alternatives Considered

### Simple CRUD

Rejected because it allows previously recorded clinical information to be
silently overwritten or deleted.

### Database-level audit only

Not sufficient as the sole mechanism because technical database auditing
does not necessarily provide an appropriate domain representation of
clinical history.

### Full Event Sourcing

Not selected or required for the initial MVP because its complexity is not
justified at the current stage of the project. It remains a possible future
evolution if requirements justify it.

The architecture should remain open to more sophisticated event-based
approaches if future requirements justify them.
