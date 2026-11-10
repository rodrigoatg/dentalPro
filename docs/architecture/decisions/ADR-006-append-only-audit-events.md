# ADR-006 — Append-Only Audit Events

## Status

Accepted for Stage 0; implementation is pending.

## Context

The product and domain documentation require traceability for appointment
creation, rescheduling, cancellation, confirmation and completion. The
repository has no AuditEvent entity, table, or generic audit service yet.
Clinical record versioning and operational audit answer different questions;
this appointment audit must not become a replacement for clinical versioning.

## Decision

- Begin with a persisted, append-only `AuditEvent` record that can be reused
  by later modules. This is not full Event Sourcing and does not require an
  event bus, projections, or recording every read.
- Each event stores an event ID, actor user ID, UTC occurrence time, stable
  action code, target type and ID, and optional related entity IDs plus small
  structured metadata. For an appointment reschedule, metadata/related IDs
  identify both the prior and replacement appointment.
- The event is written in the same database transaction as the appointment
  state changes. A failed appointment operation therefore cannot leave an
  audit event claiming that it succeeded, and a committed operation cannot
  omit its required event.
- The initial event contains only information necessary to identify the
  action and affected records. It does not duplicate patient details,
  clinical data or appointment notes.
- Normal application code may insert audit events but may not update or
  delete them. Reads of audit history are subject to separate authorization
  decisions when an endpoint is introduced.
- Application defines the operation/actor contract; Infrastructure owns the
  persistence implementation. Domain aggregates do not depend on the audit
  storage mechanism.

## Consequences

- Appointment operations must carry the authenticated actor ID into the
  application use case; it must be derived from the validated request
  principal, not from client-supplied appointment data.
- Audit write failures roll back the associated appointment change.
- The generic record can support later patient/security/clinical events
  without requiring a full event-sourced architecture.
- Audit retention, export, read authorization and any legal retention period
  remain subject to applicable regulatory and operational policy; this ADR
  does not define them.

## Alternatives Considered

### Update the appointment row only

Rejected because overwriting status or schedule fields cannot by itself
preserve the actor and action history.

### Store complete appointment snapshots in every event

Rejected for the initial implementation because it needlessly duplicates
personal or clinical context. The event needs identifiers and action metadata
to establish the trace; current appointment state remains in its aggregate.

### Full Event Sourcing

Not selected. The current scope needs append-only audit evidence, not replay
of all application state from a complete event stream.
