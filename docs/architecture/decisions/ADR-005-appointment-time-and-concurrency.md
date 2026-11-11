# ADR-005 — Appointment Time and Concurrency

## Status

Accepted for Stage 0; implementation is pending.

## Context

Appointments must not overlap for either the same Dentist or the same
Patient. Adjacent appointments are allowed. Cancelled and Rescheduled records
remain historical but do not block availability; Completed appointments
remain blocking to prevent a duplicate booking over the same interval.
Creation and rescheduling must remain correct when requests execute
concurrently.

The current `Appointment` uses `DateTimeOffset`, but PostgreSQL and Npgsql
distinguish local wall-clock schedule values from absolute instants. PostgreSQL
`timestamp with time zone` stores an instant in UTC and does not retain the
originating zone ID. Npgsql requires `DateTimeOffset` values written as
`timestamp with time zone` to have offset zero.

## Decision

### Time and clinic schedule

- A required clinic configuration contains an IANA `ClinicTimeZoneId`, a
  positive `DefaultAppointmentDurationMinutes`, recurring weekly opening
  intervals, and date exceptions. No specific time zone is selected in this
  ADR; the clinic's configured IANA ID is required before scheduling is
  enabled.
- Weekly intervals are local wall-clock times grouped by weekday. Date
  exceptions identify a local calendar date and either close the clinic or
  replace the normal intervals for that date. Holidays are manually managed
  date exceptions; no external calendar integration is added.
- The first version has one clinic-wide default duration and no appointment
  types or Dentist-specific schedules. Each booked Appointment persists its
  resolved start and end, so changing the default affects only future
  bookings.
- An appointment must fit wholly inside one open interval. A closure
  overrides the recurring schedule. Schedule changes do not silently alter
  existing appointments; affected bookings are surfaced for explicit
  resolution.
- Browser input and display use the configured clinic time zone, not the
  browser's local zone. API appointment timestamps are exchanged as ISO 8601
  values with an explicit offset and normalized to UTC before persistence.
- Appointment start and end are stored as PostgreSQL `timestamptz` UTC
  instants. The IANA zone ID is stored separately as clinic configuration.
  Recurring opening times are stored as local `time without time zone` values;
  exception dates use `date`. `time with time zone` is not used for recurring
  schedules because a time of day alone cannot resolve daylight-saving
  offsets.
- A local time that is ambiguous or does not exist due to a daylight-saving
  transition is rejected with a validation response. The application must
  not silently choose an offset or shift the time.
- All appointment intervals use the half-open form `[start, end)`. Therefore,
  intervals that meet exactly at an end/start boundary do not overlap.

### Concurrent availability enforcement

The database is the final authority for overlap prevention. The recommended
implementation is two PostgreSQL GiST exclusion constraints over the
appointment interval: one comparing `DentistId` equality plus interval
overlap, and one comparing `PatientId` equality plus interval overlap. The
constraints are partial and apply to `Scheduled`, `Confirmed` and `Completed`
only. The interval expression is a `tstzrange` with `[)` bounds. The
`btree_gist` extension supplies GiST equality operator classes for UUIDs.

`btree_gist` is a trusted PostgreSQL extension and can be installed by a
non-superuser who has `CREATE` privilege on the database. The extension files
must be available in the PostgreSQL 17 image. The migration/deployment
principal must have that database privilege, or an operator must provision
the extension before applying the migration. The provider-specific exclusion
SQL stays in Infrastructure migrations; Domain and Application remain
provider-independent.

Availability queries and clinic-hours validation remain necessary for useful
responses, but they are not the concurrency guarantee. A violation of either
exclusion constraint is translated to the same application conflict result
as a detected availability conflict.

Rescheduling is one database transaction: mark the current appointment
`Rescheduled`, create its replacement, and add the corresponding audit
record. If validation, either exclusion constraint, or any write fails, the
entire transaction rolls back. Cancelled and Rescheduled rows are omitted
from both blocking constraints; Completed rows remain included.

Updates to an existing Appointment also acquire a PostgreSQL row lock with
`SELECT ... FOR UPDATE` inside the active unit-of-work transaction. Under the
default `READ COMMITTED` isolation level, a writer waits for a concurrent
update and then reads the committed row version before applying its domain
transition. The lock is held through commit or rollback. It serializes changes
to that Appointment only; it does not lock a Dentist's or Patient's schedule
and does not replace the exclusion constraints. Longer transactions increase
contention, and operations that lock multiple appointments would need a stable
lock order to limit deadlocks.

## Alternatives Considered

### Availability query under the default transaction isolation only

Rejected as the sole protection. Two requests can both observe an available
interval before either inserts its appointment.

### Serializable transaction for every booking

This can prevent non-serializable committed outcomes when all writers follow
the protocol, but PostgreSQL may abort a transaction with a serialization
failure. The application must retry the complete operation or return a
conflict, and correctness depends on all write paths using that isolation
level. It also adds runtime handling without providing as direct an invariant
as the exclusion constraints.

### Explicit locks on Dentist and Patient rows

This can serialize bookings when every writer locks the same rows in a stable
order. It creates contention per Dentist/Patient and requires careful
deadlock handling. A writer that forgets the lock can bypass the rule.

### PostgreSQL exclusion constraints

Selected because they enforce the interval invariant for all database
writers, including concurrent requests, while preserving half-open adjacency
semantics. They require PostgreSQL-specific migration work and the
`btree_gist` extension, which are acceptable because provider-specific
behavior is isolated in Infrastructure and PostgreSQL 17 is the initial
provider.

## Consequences

- Integration tests for overlaps and concurrent bookings must run against
  PostgreSQL; EF InMemory tests cannot verify GiST constraints or PostgreSQL
  error codes.
- The implementation must verify support for the exclusion DDL in the
  generated migration and model review. The extension and constraint are
  database provisioning requirements, not application startup actions.
- Schedule configuration changes concurrent with bookings need an explicit
  transaction/locking rule in implementation so a booking is validated
  against a consistent schedule revision.
- The clinic's actual IANA time-zone ID and default appointment duration
  remain deployment/business configuration values; neither is hard-coded by
  this decision.

## References

- [PostgreSQL 17 range types](https://www.postgresql.org/docs/17/rangetypes.html)
- [PostgreSQL 17 exclusion constraints](https://www.postgresql.org/docs/17/ddl-constraints.html)
- [PostgreSQL 17 `btree_gist`](https://www.postgresql.org/docs/17/btree-gist.html)
- [PostgreSQL 17 transaction isolation](https://www.postgresql.org/docs/17/transaction-iso.html)
- [Npgsql date/time handling](https://www.npgsql.org/doc/types/datetime)
