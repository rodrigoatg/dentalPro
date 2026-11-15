# DentalPro — Domain Model

## 1. Overview

The domain model represents the main concepts involved in the
operation of a dental clinic.

The MVP implementation scope uses the following existing domain concepts:

- Users and access control.
- Patients.
- Appointments.
- Medical records and versioned ClinicalEntry records.
- Clinical treatments, including partial completion.
- A simplified Odontogram with traceable state changes.
- AuditEvent records for relevant operations.

Future capabilities include:

- Financial management.
- Reports.
- Medical images.
- Notifications.
- Clinical document export.
- Consent management.
- External healthcare integrations, FHIR, SNOMED CT and a patient portal.

This scope section does not add, remove or rename domain entities. Concepts
described elsewhere in this model as future remain outside the MVP.

## 2. Main Aggregates

### Patient

Represents a patient receiving care at the dental clinic.

Responsibilities:

- Maintain personal identification information.
- Maintain contact information.
- Own exactly one MedicalRecord, created with the Patient.
- Serve as the owner of the patient's appointment history.

Attributes:

- Id
- DocumentType
- DocumentNumber
- FirstName
- LastName
- DateOfBirth
- Phone
- Email
- Address
- IsActive
- CreatedAt
- UpdatedAt

Relationships:

- A Patient can have zero or many Appointments.
- A Patient has exactly one MedicalRecord.
- The MedicalRecord has exactly one Odontogram.

Business rules:

- DocumentType and DocumentNumber identify the patient from a business perspective.
- Their combination must be unique in persistence.
- FirstName and LastName are required.
- DateOfBirth cannot be in the future.
- A Patient is not physically deleted during normal operation. Deactivation is handled through IsActive.
- Patient data must not contain clinical information that belongs to the MedicalRecord or other clinical entities.
- Creating a Patient creates its MedicalRecord and empty Odontogram as one domain set and one transactional persistence operation.
- Creation audit is required by the MVP but remains pending; no complete audit subsystem is included in this slice.

MVP scope: Patient, MedicalRecord and an initially empty Odontogram are created together. No individual dental-piece rows are created in this slice.

Future relationship:

- A Patient may be associated with a User account in a future patient-portal
  capability. Patient accounts and patient-scoped authorization are outside
  the current MVP.
### Appointment

Represents a scheduled appointment between a patient and a dentist.

Implementation status: `DentalPro.Domain.Appointments.Appointment` and its
Application use cases and Infrastructure persistence are implemented. API
endpoints and Angular screens remain outside the current implementation stage.

Attributes:

- Id
- PatientId
- DentistId
- StartDateTime
- EndDateTime
- DurationMinutes (effective duration in whole minutes)
- Status
- Notes
- RescheduledFromAppointmentId (optional; references the immediately previous appointment in a rescheduling chain)
- CreatedAt
- UpdatedAt

Possible statuses:

- Scheduled
- Confirmed
- Completed
- Cancelled
- Rescheduled

Relationships:

- An Appointment belongs to exactly one Patient.
- An Appointment is assigned to exactly one Dentist.
- The Dentist is represented by a User with the Dentist role.
- A rescheduled replacement may reference the appointment it replaces. Following these references backwards reconstructs the complete rescheduling chain; a rescheduled appointment has at most one direct replacement.
- An Appointment is validated against the clinic's configured working hours.
- Appointment changes are recorded through the audit mechanism.

Business rules:

- An appointment must belong to a patient.
- An appointment must have a dentist.
- StartDateTime must be before EndDateTime.
- DurationMinutes is positive and `EndDateTime = StartDateTime + DurationMinutes`.
- The effective duration is persisted per appointment; later changes to the
  clinic default do not alter existing appointments.
- A dentist cannot have overlapping active appointments.
- A patient cannot have overlapping active appointments.
- An appointment must fall within the configured clinic working hours.
- Intervals use the half-open convention [start, end): consecutive appointments may share an end/start boundary without overlapping.
- Scheduled, Confirmed and Completed appointments block the corresponding patient and dentist intervals. Cancelled and Rescheduled appointments are retained but do not block availability.
- Before creation or rescheduling, the patient must exist and be active; the assigned user must exist, be active and have the Dentist role.
- Availability must be checked again in the write transaction. Persistence must independently prevent conflicting intervals under concurrent requests.
- Rescheduling marks the current appointment Rescheduled and creates a replacement referencing it. Both changes and their audit data are committed atomically; any failure rolls the complete operation back.
- Appointment status transitions are: Scheduled -> Confirmed, Completed, Cancelled or Rescheduled; Confirmed -> Completed, Cancelled or Rescheduled. Completed, Cancelled and Rescheduled are terminal states.
- Marking Completed preserves the historical appointment interval; it cannot be replaced by another blocking appointment for the same patient or dentist.
- Appointment creation, rescheduling, cancellation, confirmation and completion must be auditable. Audit data identifies the actor, UTC timestamp, action and affected appointment; rescheduling also identifies the replacement relationship.

### Clinic schedule configuration

Clinic scheduling configuration is a separate operational aggregate, persisted
by Infrastructure as one clinic-wide configuration. Application exposes
configuration use cases and the schedule query; HTTP administration endpoints
are not part of this stage. There are no implicit settings or seeded opening
hours. Until an Administrator configures the clinic, schedule-dependent
availability, creation and rescheduling fail with a not-configured error.

- One clinic time zone identified by an IANA time-zone ID. No specific zone is
  assumed by the product documentation; configuration must provide it before
  scheduling is enabled.
- A positive clinic-wide default duration and a positive start increment, both
  in minutes. The increment is independent and is only metadata for future UI
  suggestions; Application returns continuous opening intervals and does not
  generate discrete slots. A booking may use any positive effective duration,
  including values that are not multiples of the increment. Creation uses the
  configured default when no duration is supplied; rescheduling preserves the
  previous duration unless a new one is supplied. Each appointment persists
  its effective duration, so changes to the default do not rewrite existing
  appointments. Dentist-specific schedules remain outside the initial scope.
- A recurring weekly schedule containing one or more local-time opening
  intervals per day. Each interval has a start before its end; intervals on
  the same day do not overlap.
- Date exceptions that either close the clinic or replace that date's weekly
  intervals. There is at most one effective exception per local date.
  Holidays are entered as date exceptions; no external holiday integration
  is introduced.
- Weekly intervals and exception intervals must be within one local calendar
  day, start before end, and not overlap. An exception with no intervals is
  an explicit closure.
- A booking interval must fit wholly inside one opening interval. A closure
  overrides the weekly schedule. Changing clinic hours does not silently move
  or delete existing appointments; affected bookings must be reviewed and
  resolved explicitly.

For persistence, recurring times are local wall-clock values associated with
the configured clinic zone, not instants. Date exceptions use a calendar date.
Appointment start/end values are UTC instants, and the effective duration is
stored in minutes and constrained to match the interval. The zone identifier
is stored separately because PostgreSQL `timestamp with time zone` stores an
instant, not the originating time-zone ID. Application interprets explicit
offset input as a clinic-local wall time, verifies the offset against the
configured IANA zone, and rejects ambiguous or nonexistent local times.
Infrastructure converts recurring and exception boundaries with that same
zone and rejects ambiguous or nonexistent boundaries; it never uses the host
or browser local zone.

Persistence uses `ClinicSchedules` (one singleton row),
`ClinicWeeklyIntervals`, `ClinicScheduleExceptions`, and
`ClinicScheduleExceptionIntervals`. Schedule changes and booking operations
use the existing shared appointment unit of work. A booking takes a PostgreSQL
`FOR SHARE` lock on the singleton schedule row; configuration changes take
`FOR UPDATE`. This keeps the configuration stable through booking commit.
Changes to hours do not move or delete existing appointments.

### MedicalRecord

Represents the longitudinal clinical record of a patient within the clinic. Each Patient has exactly one MedicalRecord, created together with the Patient.

Attributes:

- Id: internal technical Guid.
- PatientId
- MedicalRecordNumber: required business identifier once assigned; a positive sequential long, globally unique and permanent.
- StartedAt
- LastClinicalActivityAt (nullable; null until the first clinical activity)
- CreatedAt
- UpdatedAt

Relationships:

- Belongs to exactly one Patient.
- Contains exactly one Odontogram.
- Contains zero or many ClinicalEntries and Treatments.
- May contain future Consents, MedicalImages and ClinicalDocuments.

Business rules:

- A Patient has exactly one MedicalRecord within the clinic, and it must be uniquely associated with that Patient.
- MedicalRecordNumber is globally unique for one DentalPro installation, is assigned by PostgreSQL in the future persistence implementation, and cannot be changed after assignment. Domain does not generate it.
- The numeric value is stored as long. Presentation uses the prefix HC- and at least six digits (for example, HC-000123); prefix and zero-padding are not stored in the domain value.
- During domain creation, before persistence assigns the number, the number is explicitly unassigned (nullable long). It cannot be assigned a non-positive value or reassigned.
- StartedAt and CreatedAt are set to the same UTC instant at creation. UpdatedAt starts at that instant and changes when the record is modified. LastClinicalActivityAt starts null.
- Clinical information must be recorded chronologically. Previously recorded clinical information must not be silently overwritten.
- Modifications to clinical information must remain traceable.
- Clinical records are not physically deleted through normal operations.
- The MedicalRecord must remain recoverable for the applicable retention period.
- The MedicalRecord must support future interoperability requirements.
- Access to clinical information must be restricted to authorized users.

MVP scope: Creation of the MedicalRecord and empty Odontogram is included. Clinical entries, detailed clinical history, and assignment of the database-generated MedicalRecordNumber during persistence are handled in their respective implementation steps.
### ClinicalEntry

Represents an individual clinical event, observation, or intervention
recorded during patient care.

Attributes:

- Id
- MedicalRecordId
- DentistId
- AppointmentId
- TreatmentId
- ClinicalEntryType
- OccurredAt
- Notes
- CreatedAt
- UpdatedAt

Initial ClinicalEntry types:

- Consultation
- Treatment
- Observation
- FollowUp
- ExternalRecord
- Other

Relationships:

- Belongs to exactly one MedicalRecord.
- Has exactly one responsible Dentist.
- May be associated with an Appointment.
- May be associated with a Treatment.

Business rules:

- A ClinicalEntry records what occurred during a clinical encounter.
- A ClinicalEntry must preserve the date and responsible Dentist.
- AppointmentId is optional because not every clinical event must originate
  from a scheduled appointment.
- TreatmentId is optional because not every clinical event is related to
  a specific Treatment.
- ClinicalEntry should provide sufficient information to understand the
  clinical event without replacing structured Treatment data.
- ClinicalEntry does not represent the current state of the Odontogram.
- Clinical information must not be silently overwritten; changes must
  remain traceable.
- Recording a ClinicalEntry does not automatically modify the Odontogram.

Versioning:

- A ClinicalEntry is versioned through ClinicalEntryVersion.
- The current version represents the latest valid state.
- Previous versions remain available for audit and traceability.
- Previous versions cannot be modified or deleted through normal
  application operations.

MVP scope: ClinicalEntry versioning is required. Previous versions remain
preserved and accessible. This document does not prescribe the persistence
mechanism.

### ClinicalEntryVersion

Represents an immutable snapshot of a ClinicalEntry at a specific point
in time.

Each version contains the complete state of the ClinicalEntry at the
moment the version was created.

Attributes:

- Id
- ClinicalEntryId
- VersionNumber
- MedicalRecordId
- DentistId
- AppointmentId
- TreatmentId
- ClinicalEntryType
- OccurredAt
- Notes
- CreatedAt
- CreatedBy
- ChangeReason

Relationships:

- Belongs to exactly one ClinicalEntry.
- Each ClinicalEntry can contain one or many versions.

Business rules:

- ClinicalEntryVersion records are immutable.
- Updating a ClinicalEntry never overwrites an existing version.
- Each modification creates a new version.
- Each version contains the complete state of the ClinicalEntry at the
  time it was created.
- VersionNumber must be sequential within a ClinicalEntry.
- Only authorized users may create new versions.
- The system must preserve the identity of the user who created each
  version.
- The system must preserve the reason for each modification when
  required.

### AuditEvent

Represents an immutable audit record of an action performed within
the system.

Audit events provide traceability for security-sensitive,
clinical, administrative, and data-related operations.

Attributes:

- Id
- UserId
- Action
- EntityType
- EntityId
- OccurredAt
- IpAddress
- UserAgent
- CorrelationId
- Metadata

Possible actions include:

- Create
- Read
- Update
- Delete
- Login
- Logout
- Export
- Cancel
- Restore
- AssignRole
- RemoveRole
- AssignPermission
- RemovePermission
- DeactivateUser
- ActivateUser

Relationships:

- May belong to a User when the action was performed by an authenticated
  user.
- May represent a system-generated action when UserId is null.
- References the affected entity through EntityType and EntityId.

Business rules:

- AuditEvent records are immutable.
- Audit events must not be modified or deleted through normal application
  operations.
- Security-sensitive and clinical operations must generate audit events.
- Relevant clinical, patient, treatment, odontogram and authentication
  operations must be auditable. The MVP does not require auditing every
  read operation.
- Creation of new ClinicalEntryVersion records must generate an
  corresponding audit event.
- Export of clinical information must generate an audit event.
- Each audit event should contain a CorrelationId when the operation
  belongs to a distributed or multi-step request.
- Audit events must preserve the identity of the user or system process
  responsible for the action.

### Odontogram

Represents the current dental state of a patient's mouth within the
MedicalRecord and does not replace the clinical history.

The Odontogram is a current-state representation intended to provide
healthcare professionals with a quick overview of the patient's dental
condition.

The clinical evolution of dental conditions is not stored as the primary
responsibility of the Odontogram. Historical evolution is represented through
ClinicalEntries, Treatments and their associated history.

Attributes:

- Id: internal technical Guid.
- MedicalRecordId
- CreatedAt
- UpdatedAt

Responsibilities:

- Represent the current state of the patient's dental pieces.
- Identify dental pieces using the standardized dental numbering system.
- Provide a quick overview of the patient's current dental condition.
- Support updates resulting from clinical actions such as treatments.

Relationships:

- Belongs to exactly one MedicalRecord.
- Contains one OdontogramTooth for each represented dental piece.
- Has zero or many ToothStateHistory records.
  

Business rules:

- An Odontogram belongs to exactly one MedicalRecord.
- The Odontogram represents the current state of the patient's dentition.
- Historical clinical evolution must not depend exclusively on the Odontogram.
- Dental pieces must use the standardized dental numbering system.
- A Treatment does not automatically change the Odontogram merely because
  a tooth is included in the treatment.
- The Odontogram is updated only when the corresponding clinical
  intervention is effectively recorded as performed.
- Historical changes to dental states must remain traceable through
  clinical records and treatment information.
- Every change to a ToothState must generate a ToothStateHistory record.
- ToothStateHistory records are immutable.
- The current Odontogram state must be consistent with the latest valid
  ToothStateHistory for each dental piece.

MVP scope: In this first patient-creation slice, the Odontogram is created empty with no OdontogramTooth rows. Later MVP work covers permanent dentition using FDI notation and basic tooth states, with changes traceable through ToothStateHistory. Tooth surface detail is excluded.

### OdontogramTooth

Represents the current state of one dental piece within an Odontogram.

Attributes:

- Id
- OdontogramId
- ToothNumber
- State
- UpdatedAt

State values may include:

- Unknown
- Healthy
- Caries
- Restored
- Missing
- Extracted
- RootCanal
- Crown
- Other

The set of supported states is intentionally limited in the MVP and
may be expanded as the clinical requirements evolve.

Relationships:

- Belongs to exactly one Odontogram.
- Is identified by a dental piece number using FDI notation.

Business rules:

- ToothNumber must represent a valid tooth according to the configured
  dental notation.
- The current state must reflect the latest valid clinical operation.
- Changes to State must be traceable through ToothStateHistory.


### ToothStateHistory

Represents an immutable historical change to the state of a dental piece.

Attributes:

- Id
- OdontogramId
- ToothNumber
- PreviousState
- NewState
- ChangedAt
- ChangedBy
- ClinicalEntryId
- TreatmentId
- CorrelationId

Relationships:

- Belongs to one Odontogram.
- References one dental piece.
- May reference the ClinicalEntry that documents the change.
- May reference the Treatment that caused the change.
- References the User responsible for the change.

Business rules:

- Historical records are immutable.
- Every change to an OdontogramTooth state must create a corresponding
  ToothStateHistory record.
- PreviousState and NewState must be preserved.
- A state change should reference the clinical operation that caused it
  when applicable.

### TreatmentToothSurface

Represents a dental surface affected by a TreatmentTooth.

Attributes:

- TreatmentId
- ToothNumber
- Surface

Possible surfaces depend on the dental piece and may include:

- Mesial
- Distal
- Buccal
- Lingual
- Occlusal
- Incisal

This concept is reserved for future clinical detail and is outside the
initial MVP implementation.

### Dental notation

DentalPro will use the FDI tooth numbering system as the initial dental
notation.

The MVP will support permanent dentition.

The domain model must avoid coupling the application to a raw integer
representation of tooth numbers. ToothNumber will be treated as a
domain value that validates the selected dental notation.

Support for primary and mixed dentition may be added in a future
iteration without changing the fundamental Treatment, MedicalRecord,
or Odontogram architecture.

### Treatment

Represents a dental treatment performed or planned for a patient and
recorded within the patient's MedicalRecord.

A Treatment may involve zero, one, or multiple dental pieces.

Attributes:

- Id
- MedicalRecordId
- DentistId
- TreatmentType
- OccurredAt
- RecordedAt
- Description
- Priority
- Status
- Cost
- CreatedAt
- UpdatedAt

Possible statuses:

- Planned
- InProgress
- PartiallyCompleted
- Completed
- Cancelled

Possible priorities:

- Low
- Medium
- High
- Urgent

Relationships:

- Belongs to exactly one MedicalRecord.
- Has exactly one responsible Dentist.
- Has one TreatmentType.
- May involve zero or many dental pieces through TreatmentTooth.

Business rules:

- A Treatment must belong to a MedicalRecord.
- A Treatment must have a responsible Dentist.
- A Treatment may involve multiple dental pieces.
- A dental piece cannot be associated more than once with the same Treatment.
- The inclusion of a dental piece in a Treatment does not imply that
  its dental state has been modified.
- The dental state is updated only when the corresponding clinical
  intervention is effectively recorded as performed.
- A Treatment may be partially completed when some of its associated
  dental pieces have been treated while others remain pending.
- Changes to a Treatment must remain traceable.
- Changes affecting the current dental state must remain traceable.
- Cost represents clinical treatment cost information and does not
  represent a financial transaction in the current MVP.

MVP scope: Treatment recording and partial treatment support are included.
The original treatment must retain the completion state that was true at
that time. A later clinical action that completes a previously pending
tooth must be recorded as a later treatment/action and must not rewrite the
original treatment's history.


### TreatmentTooth

Represents the participation and progress of a dental piece within a
Treatment.

Attributes:

- TreatmentId
- ToothNumber
- Status
- CompletedAt
- Notes
- ResultingToothState

Possible statuses:

- Planned
- InProgress
- Completed
- Cancelled

Relationships:

- Belongs to one Treatment.
- References one dental piece.
- May contain zero or many TreatmentToothSurface records.

Business rules:

- A Treatment may contain multiple TreatmentTooth records.
- Each TreatmentTooth represents one dental piece.
- Different TreatmentTooth records within the same Treatment may have
  different statuses.
- The overall Treatment status may be derived from the status of its
  TreatmentTooth records when applicable.
- Completing a TreatmentTooth does not directly modify the Odontogram.
- A completed clinical operation may produce a new ToothState in the
  Odontogram.

### Example: Partially Completed Treatment

A dentist plans a restoration involving teeth 14, 15 and 16.

The Treatment is created as:

- TreatmentType: Restoration
- Status: InProgress

The dentist successfully restores teeth 14 and 15, but treatment of
tooth 16 cannot be completed due to an unexpected interruption.

The system records:

- Tooth 14: Completed
- Tooth 15: Completed
- Tooth 16: Planned

The Treatment status becomes:

- PartiallyCompleted

A ClinicalEntry records the event and explains why tooth 16 remains
pending.

The Odontogram is updated only for teeth 14 and 15.

Later, the patient attends another appointment and a subsequent Treatment B
records the restoration of tooth 16. Treatment A remains historically
accurate with tooth 16 pending. The system records:

- Treatment B, tooth 16: Completed

The Odontogram is then updated to reflect the new current state of
tooth 16.

This preserves the distinction between planned treatment, performed
clinical actions, and the current dental state.

### TreatmentType

Represents the clinical category of a dental treatment.

Initial values:

- Restoration
- Extraction
- RootCanal
- Cleaning
- Crown
- Other

TreatmentType provides structured classification while Description
contains additional clinical details.

### ToothState

Represents the current clinical state of a dental piece within a
patient's Odontogram.

Attributes:

- OdontogramId
- ToothNumber
- State
- UpdatedAt

Initial states may include:

- Healthy
- Caries
- Restored
- Missing
- Extracted
- RootCanal
- Crown
- Other
- Unknown

Business rules:

- A ToothState represents the current state of one dental piece.
- The current state is updated only as a result of a valid clinical
  operation.
- Changes to the current state must be traceable through
  ToothStateHistory.

  ### ToothStateHistory

Represents a historical change to the clinical state of a dental piece.

Attributes:

- Id
- OdontogramId
- ToothNumber
- PreviousState
- NewState
- ChangedAt
- ChangedBy
- ClinicalEntryId
- TreatmentId
- CorrelationId

Relationships:

- Belongs to one Odontogram.
- References one dental piece.
- May reference the ClinicalEntry that documented the change.
- May reference the Treatment that caused the change.
- References the user responsible for the change.

Business rules:

- Historical records are immutable.
- Every change to a current ToothState must create a corresponding
  ToothStateHistory record.
- The history must preserve both the previous and new state.
- A state change must be associated with the clinical operation that
  caused it when applicable.

### User

Represents an individual account that can authenticate to DentalPro. User
credentials and role membership are managed through ASP.NET Core Identity.

Attributes:

- Id
- Username
- Email
- PasswordHash
- IsActive
- FirstName
- LastName
- Phone
- CreatedAt
- UpdatedAt
- LastLoginAt

Relationships:

- A User may have one or more Roles through Identity's user-role association.
- Permissions are assigned to Roles only; there are no user-specific
  permissions.
- A User's effective permissions are the union of permissions assigned to
  all of that user's Roles.
- Authorization uses current role-permission assignments stored by the
  application, not permission claims from a previously issued access token.
- A change to role permissions applies to the next authorized request and
  does not require the user to sign in again.
- Deactivating a user or changing the user's roles invalidates previously
  issued access tokens through security-state validation.


### Role

Represents one of the fixed system roles to which users may be assigned and
permissions may be associated.

Initial MVP roles:

- Administrator
- Receptionist
- Dentist


Relationships:

- A Role can be assigned to many Users through Identity's user-role
  association, and a User can have multiple Roles.
- A Role can contain many Permissions through RolePermission.
- Role names and membership are not permission definitions. Possessing the
  Administrator role does not automatically grant clinical permissions.
- When clinical endpoints are implemented, their initial permissions are
  assigned to Dentist only; no clinical permission is part of the initial
  catalog because there are no clinical endpoints yet.
- Role definitions are fixed for the MVP; users with protected administrative
  permissions may configure role-permission assignments through the API.

### Permission

Represents a backend capability that can be granted to a Role. The permission
catalog has one canonical definition in backend code and is seeded into
persistence. Permission codes are stable technical identifiers used for
authorization. Names, descriptions and module labels are presentation
metadata and may change without changing authorization behavior.

Only permissions corresponding to implemented API endpoints belong in the
active catalog. Adding a permission does not itself implement an endpoint or
grant that permission to a role.

The initial MVP catalog is:

Permission properties:

- Id: Guid generated when the catalog entry is created. Seeding resolves
  entries by Code and preserves an existing Id.
- Code: required, unique, lowercase dot-separated stable authorization key.
- Name: required display name; may change without changing authorization.
- Description: optional display text; may change without changing
  authorization.
- Module: required display/grouping metadata; may change without changing
  authorization.

The domain prevents changing Code through its metadata update operation.
Uniqueness across catalog entries is enforced by the catalog and persistence
constraints, not by an individual Permission instance.

| Code | Meaning | Module |
| --- | --- | --- |
| `patients.view` | List patients | Patients |
| `patients.create` | Create patients | Patients |
| `users.view` | List and inspect user accounts | Identity |
| `users.create` | Create user accounts | Identity |
| `users.status.manage` | Activate or deactivate user accounts | Identity |
| `users.roles.manage` | Assign or remove roles from users | Identity |
| `roles.view` | List roles and inspect their permission assignments | Identity |
| `roles.permissions.manage` | Assign or remove permissions on roles | Identity |
| `permissions.view` | View the backend-defined permission catalog | Identity |

The patient catalog is limited to the current list and create endpoints;
there is no `patients.edit` permission because no patient-edit endpoint is
implemented. Appointment, clinical-record and treatment permissions are
excluded until corresponding endpoints exist.

Protected administrative permissions are defined in backend code and cannot
be removed from Administrator by any normal API operation:

- `users.view`
- `users.create`
- `users.status.manage`
- `users.roles.manage`
- `roles.view`
- `roles.permissions.manage`
- `permissions.view`

The API must enforce this invariant independently of client input. Seeding
must ensure that protected assignments exist and must not silently delete
existing role-permission assignments.

Initial role-permission matrix:

| Permission | Administrator | Dentist | Receptionist |
| --- | --- | --- | --- |
| `patients.view` | Yes | Yes | Yes |
| `patients.create` | Yes | Yes | Yes |
| `users.view` | Yes (protected) | No | No |
| `users.create` | Yes (protected) | No | No |
| `users.status.manage` | Yes (protected) | No | No |
| `users.roles.manage` | Yes (protected) | No | No |
| `roles.view` | Yes (protected) | No | No |
| `roles.permissions.manage` | Yes (protected) | No | No |
| `permissions.view` | Yes (protected) | No | No |

This is the initial assignment. Other non-protected assignments may be
configured for roles as the implemented API grows. Administrator receives
no clinical permissions by default.

### Scheduling permissions

The following codes are present in the backend catalog and seeded additively
by the security bootstrap. HTTP endpoints remain pending.

| Code | Meaning | Initial assignment |
| --- | --- | --- |
| `appointments.view` | View the authenticated Dentist's own agenda; required as the base agenda-view permission | Administrator, Receptionist, Dentist |
| `appointments.view.all` | Expand agenda scope to all Dentists in the clinic | Administrator, Receptionist |
| `appointments.create` | Create an appointment for an existing active Patient and active Dentist | Administrator, Receptionist |
| `appointments.update` | Reschedule an appointment by retaining the prior record and creating its replacement | Administrator, Receptionist |
| `appointments.cancel` | Cancel an appointment while retaining its record | Administrator, Receptionist |
| `appointments.confirm` | Confirm an appointment within the caller's authorized agenda scope | Administrator, Receptionist, Dentist |
| `appointments.complete` | Mark an appointment within the caller's authorized agenda scope as attended | Administrator, Receptionist, Dentist |
| `appointments.schedule.manage` | Configure clinic opening hours, appointment duration and date exceptions | Administrator |

`appointments.view.all` expands data scope; it does not grant create, update,
cancel, confirm, complete or schedule-management operations by itself. Each
operation still requires its own permission. Administrator's agenda grants
are explicit initial role-permission assignments, not an implicit effect of
the Administrator role, and they do not make clinical permissions
Administrator-protected. Receptionist can administer the general agenda but
does not configure clinic hours. Without `appointments.view.all`, confirm and
complete operations must verify that the appointment's DentistId is the
authenticated user's ID. Users with multiple roles receive the union of
the explicitly assigned permissions, as elsewhere in RBAC.


Relationships:

- A Permission can belong to many Roles through RolePermission.
- Permission codes are unique and stable. Permission metadata may change
  independently of the code.
- Permissions are never assigned directly to Users.

### UserRole

Identity-managed association between User and Role. A user may have multiple
roles and each role may be assigned to multiple users.

Attributes:

- UserId
- RoleId

### RolePermission

Associative entity between Role and Permission.

Each RolePermission associates exactly one Role with one Permission. The
RoleId/PermissionId pair is its composite identifier and must be unique so
that duplicate assignments cannot be stored. RoleId is an opaque Guid that
matches the future Identity role key; the domain does not reference Identity
types. Both identifiers are required and cannot be empty.

Attributes:

- RoleId
- PermissionId

User
 │
 └──< Identity user-role association >── Role
                                           │
                                           └──< RolePermission >── Permission

### RBAC security invariants

- Effective permissions equal the union of permissions assigned to all
  current roles of the authenticated user.
- The API evaluates current role-permission assignments on every
  authorization decision. Permission claims in a JWT are not an authority
  for granting access.
- Role-permission changes take effect on the next request and require no new
  sign-in. The MVP does not cache effective permissions.
- No normal API operation may remove protected administrative permissions
  from Administrator.
- An active Administrator may not remove their own administrative role or
  deactivate themselves through normal API operations.
- The last active Administrator cannot lose the Administrator role or be
  deactivated. Concurrent account/role changes must preserve this invariant.
- Deactivation and role changes invalidate previously issued JWTs by
  changing and validating the user's security state. Validation failure or
  inability to verify current state must reject the request.
- The initial installation creates the fixed roles and initial permission
  assignments idempotently, then bootstraps its first Administrator from
  deployment secrets. No public bootstrap or registration endpoint exists.

## 3. Future Domain Concepts

### MedicalImage

Represents a clinical image or document associated
with a patient's medical record.

### FinancialTransaction

Represents an income or outstanding debt associated
with a patient.

### FinancialReport

Represents a consolidated financial report.

### Notification

Represents a notification generated by the system,
for example after an appointment is confirmed.

### Consent

Represents an informed consent associated with a clinical procedure,
treatment or other healthcare action.

Potential attributes:

- Id
- MedicalRecordId
- TreatmentId
- PatientId
- ConsentType
- Status
- GrantedAt
- RevokedAt
- DocumentReference
- CreatedAt

Possible statuses:

- Pending
- Granted
- Rejected
- Revoked

Purpose:

- Record the patient's informed consent when required.
- Maintain evidence of consent associated with a clinical action.
- Preserve the history of consent and revocation events.

Implementation of this concept is planned for a future version.

### ClinicalDocument

Represents a clinical document associated with a patient's MedicalRecord.

Examples:

- Clinical reports.
- External consultation documents.
- Laboratory results.
- Other clinically relevant documents.

Relationships:

- Belongs to exactly one MedicalRecord.
- May be associated with a ClinicalEntry or Treatment.

Implementation of this concept is planned for a future version.

## ClinicalEvent

Represents a conceptual chronological clinical event within a
patient's MedicalRecord.

A ClinicalEvent may represent an action, observation, treatment,
diagnosis, consent or other clinically relevant event.

This concept is currently considered a domain modeling option rather
than a persisted entity.

Before implementation, the team will evaluate whether ClinicalEvent
should become a persistent aggregate/entity or remain a conceptual
abstraction over existing clinical entities.

The decision will be documented through an Architecture Decision Record.


## 4. Cross-Cutting Domain Concerns

### Auditability

The system must provide traceability for operations involving clinical,
patient, security, and administrative information.

The intended audit design uses two complementary mechanisms:

- Clinical versioning preserves the complete state of clinical records
  at each version.
- AuditEvent records who performed an operation, when it occurred, what
  entity was affected, and additional contextual information.

The AuditEvent concept is a requirement, not an existing implementation: no
AuditEvent entity, table, or generic audit service is currently present in
the source code. The initial appointment implementation will use an append-only
audit record written in the same database transaction as each appointment
change. It records actor user ID, UTC occurrence time, action, affected
appointment ID, and (for rescheduling) the prior/replacement appointment IDs.
It stores only the contextual data needed for traceability and does not copy
patient clinical data or appointment notes. This is a minimal reusable event
record, not Event Sourcing; later modules may emit the same event contract and
storage can be generalized without changing appointment history semantics.

MVP audit scope includes patient creation, update and deactivation; appointment creation, update and cancellation; clinical record changes; ClinicalEntry and Treatment creation or update; Odontogram changes; and authentication events. Patient-creation audit remains a documented requirement pending implementation; the initial patient-creation slice does not implement a complete audit subsystem. Every read operation does not need an AuditEvent in the MVP.

Clinical versions and audit events serve different purposes and must
not be treated as interchangeable.

Clinical versioning answers:

"What did the clinical record contain at a specific point in time?"

Audit logging answers:

"Who performed an operation on the system, when, and on which resource?"
