# ADR-007 — Clinical entry treatment cardinality and initial version

## Status

Accepted

## Context

The clinical-record MVP records one clinical encounter and can document more
than one treatment in that encounter. The earlier domain text described an
optional singular Treatment association, which could not represent this
workflow. ADR-002 also requires preserved ClinicalEntry versions.

## Decision

- A ClinicalEntry may reference zero or many Treatments.
- A ClinicalEntryVersion stores a complete snapshot, including zero or many
  Treatment references.
- The MVP creates immutable version 1 with each ClinicalEntry. There is no
  edit endpoint in this slice; any future amendment must append a new version.
  PostgreSQL rejects updates and deletes of version rows and their treatment
  association rows. It permits a version's treatment links only in the same
  transaction that inserts the version.
- Treatment status continues to include `PartiallyCompleted`; where treatment
  progress is tracked per tooth, the aggregate status reflects that progress.
- Initial clinical permissions are granted to Dentist by the security
  bootstrap. Administrator and Receptionist receive no clinical permissions
  automatically; administrators can grant explicit permissions through RBAC.
- A dentist's record scope is established by an appointment for the patient
  or a prior clinical entry authored by that dentist. `clinical.records.view.all`
  expands scope when explicitly assigned.

## Consequences

The persistence model uses association tables for current ClinicalEntry
treatments and the treatment IDs captured by each version. This preserves the
many-to-many encounter relationship without putting multiple IDs in a text or
JSON column. The MVP preserves treatment history but does not yet expose
amendment or odontogram-update workflows.
