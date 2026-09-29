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
- Provide access to the patient's medical record.
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
- A Patient has one Odontogram through the MedicalRecord.

Business rules:

- DocumentType and DocumentNumber identify the patient from a
  business perspective.
- The combination of DocumentType and DocumentNumber must be unique.
- FirstName and LastName are required.
- DateOfBirth cannot be in the future.
- A Patient is not physically deleted during normal operation.
  Deactivation is handled through IsActive.
- Patient data must not contain clinical information that belongs to
  the MedicalRecord or other clinical domain entities.
- Creation of a Patient should create the associated MedicalRecord
  and Odontogram as part of the same transactional operation.

MVP scope: Patient and MedicalRecord creation are included. The Odontogram
created for the MVP uses the simplified scope defined below.

Future relationship:

- A Patient may optionally be associated with a User account.
- A Patient User account is assigned the Patient role.
- Patient users can only access resources belonging to themselves.

### Appointment

Represents a scheduled appointment between a patient and a dentist.

Attributes:

- Id
- PatientId
- DentistId
- StartDateTime
- EndDateTime
- Status
- Notes

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
- An Appointment is validated against the clinic's configured working hours.
- Appointment changes are recorded through the audit mechanism.

Business rules:

- An appointment must belong to a patient.
- An appointment must have a dentist.
- StartDateTime must be before EndDateTime.
- A dentist cannot have overlapping active appointments.
- A patient cannot have overlapping active appointments.
- An appointment must fall within the configured clinic working hours.
- Cancelled appointments do not block the corresponding time slot.
- Appointment status transitions must follow the defined business rules.

### MedicalRecord

Represents the longitudinal clinical record of a patient within the clinic.

The MedicalRecord is the unique clinical record associated with a patient
within the clinic. It provides the chronological structure for recording
clinical care and related information.

Responsibilities:

- Maintain the patient's longitudinal clinical information.
- Provide a chronological record of clinical care.
- Associate clinical information with the responsible healthcare professional.
- Preserve the integrity and traceability of clinical information.
- Provide controlled access to clinical information.
- Support the Odontogram and future clinical documents, consents and medical images.

Attributes:

- Id
- PatientId
- MedicalRecordNumber
- StartedAt
- LastClinicalActivityAt
- CreatedAt
- UpdatedAt

Relationships:

- Belongs to exactly one Patient.
- Contains zero or many ClinicalEntries.
- Contains zero or many Treatments.
- Contains exactly one Odontogram.
- May contain zero or many Consents.
- May contain zero or many MedicalImages.
- May contain zero or many ClinicalDocuments.

Business rules:

- A Patient has exactly one MedicalRecord within the clinic.
- The MedicalRecord must be uniquely associated with its Patient.
- Clinical information must be recorded chronologically.
- Previously recorded clinical information must not be silently overwritten.
- Modifications to clinical information must remain traceable.
- Access to clinical information must be restricted to authorized users.
- The MedicalRecord must remain recoverable for the applicable retention period.
- The MedicalRecord must support future interoperability requirements.

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

- Id
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

MVP scope: The initial implementation covers permanent dentition using FDI
notation and basic tooth states. It does not include tooth surface detail.
Changes to tooth state remain historically traceable through the existing
ToothStateHistory concept.

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

Represents a system user.

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

- A User can have one or more Roles through UserRole.
- A User can perform actions according to the permissions
  associated with its Roles.


### Role

Represents a set of permissions assigned to a user.

MVP roles:

- Receptionist
- Dentist
- Administrative
- SystemAdministrator


Relationships:

- A Role can be assigned to many Users through UserRole.
- A Role can contain many Permissions through RolePermission.

Role categories:

Operational roles:
- Receptionist
- Dentist
- Administrative

System administration:
- SystemAdministrator

Technical development/testing account:
- PlatformAdministrator

### PlatformAdministrator

Represents a privileged developer/superuser account used for development,
testing, maintenance or troubleshooting. It is not a normal operational
business role.

This role is intended for development, maintenance, troubleshooting,
and platform administration.

It is not considered a regular operational role of the dental clinic.

PlatformAdministrator accounts must not be used as the primary
accounts for validating functional role-based access control.
Functional authorization must also be tested using accounts assigned
to the corresponding operational roles.

### Permission

Represents an operation that can be performed in the system.

Examples:

- patients.read
- patients.create
- appointments.read
- appointments.create
- clinical.read
- clinical.write
- users.manage


Relationships:

- A Permission can belong to many Roles through RolePermission.

### UserRole

Associative entity between User and Role.

Attributes:

- UserId
- RoleId

### RolePermission

Associative entity between Role and Permission.

Attributes:

- RoleId
- PermissionId

User
 │
 └──< UserRole >── Role
                    │
                    └──< RolePermission >── Permission

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

Auditability is implemented through two complementary mechanisms:

- Clinical versioning preserves the complete state of clinical records
  at each version.
- AuditEvent records who performed an operation, when it occurred, what
  entity was affected, and additional contextual information.

MVP audit scope includes patient creation, update and deactivation;
appointment creation, update and cancellation; clinical record changes;
ClinicalEntry and Treatment creation or update; Odontogram changes; and
authentication events. Every read operation does not need an AuditEvent in
the MVP.

Clinical versions and audit events serve different purposes and must
not be treated as interchangeable.

Clinical versioning answers:

"What did the clinical record contain at a specific point in time?"

Audit logging answers:

"Who performed an operation on the system, when, and on which resource?"
