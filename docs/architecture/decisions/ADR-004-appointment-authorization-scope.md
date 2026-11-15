# ADR-004 — Appointment Authorization Scope

## Status

Accepted; Application authorization and additive bootstrap assignments are implemented. HTTP endpoints remain pending.

## Context

The current RBAC model grants capabilities through explicit role-permission
assignments, computes each user's effective permissions as their union, and
requires the API to check current assignments. The active permission catalog
contains only capabilities backed by implemented endpoints. The scheduling
requirements additionally distinguish a Dentist's own agenda from the
clinic-wide agenda managed by Administrator and Receptionist.

A capability check alone does not restrict which appointment rows a caller
may read or mutate. Angular visibility is not an authorization boundary.
Administrator also has no clinical permissions by default, so scheduling
access must be seeded explicitly rather than inferred from the role name.

## Decision

1. `appointments.view` is required to enter the agenda. Without the
   clinic-wide scope permission, reads and any permitted create/update/cancel
   operation are restricted to the authenticated user's own DentistId.
2. `appointments.view.all` expands resource scope to all Dentists in the
   clinic for reads and for operations whose separate action permission the
   caller also has. It is initially assigned only to Administrator and
   Receptionist. It does not grant any appointment action by itself.
3. The initial assignments for implemented scheduling capabilities are:

   | Role | Initial appointment permissions |
   | --- | --- |
   | Administrator | `appointments.view`, `appointments.view.all`, `appointments.create`, `appointments.update`, `appointments.cancel`, `appointments.confirm`, `appointments.complete`, `appointments.schedule.manage` |
   | Receptionist | `appointments.view`, `appointments.view.all`, `appointments.create`, `appointments.update`, `appointments.cancel`, `appointments.confirm`, `appointments.complete` |
   | Dentist | `appointments.view`, `appointments.confirm`, `appointments.complete` |

4. `appointments.update` means reschedule. Dentist does not initially receive
   create, update or cancel permission. A caller without clinic-wide scope
   may create only for their own DentistId and may update/cancel/confirm/
   complete only appointments assigned to their own DentistId. A caller with
   `appointments.view.all` may act on any Dentist's appointment only when
   they also have the corresponding action permission. Application/API must
   perform both checks on every request.
5. `appointments.schedule.manage` is an additional permission required by
   FR-006 for configuring clinic hours and exceptions. It is assigned
   explicitly to Administrator. It is not marked as a protected Identity
   administration permission.
6. These assignments are initial, additive bootstrap assignments. They use
   the existing role-permission mechanism and do not change the global RBAC
   policy. Bootstrap creates missing permissions and assignments without
   deleting existing assignments or changing user credentials/status. They
   are not grants based solely on role names. The catalog and Application
   checks are implemented; HTTP endpoint authorization remains pending.
7. Appointment queries and mutations apply the caller's scope on the server.
   A supplied `dentistId` filter may narrow an authorized query but may never
   broaden the caller's scope. The frontend may hide actions using effective
   permissions but is not trusted to enforce authorization.
8. Users with multiple roles continue to receive the union of their
   explicitly assigned permissions. There is no role-priority rule.

## Consequences

### Positive

- Own-agenda and general-agenda access have explicit, reviewable permissions.
- The backend enforces ownership independently of the Angular interface.
- Administrator scheduling rights do not weaken the rule that Administrator
  receives no clinical permissions implicitly.
- Existing dynamic permission policies and additive bootstrap behavior can be
  reused.

### Negative

- General-scope requests require an additional current-permission check in
  the application query, in addition to the base agenda permission.
- The code-defined catalog, endpoint requirements and bootstrap assignments
  must be changed together when scheduling endpoints are implemented.
- The initial role matrix must be reviewed if users are assigned multiple
  roles, because the existing union semantics intentionally combine grants.

## Alternatives Considered

### Use only `appointments.view` and infer scope from role names

Rejected. Role identity would implicitly grant a data scope and would not
make that grant configurable through the established permission model.

### Enforce scope only in Angular

Rejected. Client-side visibility is not a security boundary, and API clients
can construct requests directly.

### Give every appointment permission to Dentist

Rejected for the initial version because the approved role scope explicitly
limits Dentist to viewing, confirming and completing their own appointments.
