# DentalPro — Regulatory Requirements

## 1. Purpose

This document identifies the main regulatory considerations relevant to
DentalPro's management of clinical information.

The document is intended to guide architectural and functional decisions
during development.

It does not constitute legal advice or a complete compliance assessment.
Product scope decisions in this document describe implementation planning;
they do not define, limit or replace obligations arising from applicable
law. Those obligations require separate validation.

---

## 2. Applicable Regulatory Framework

The system is designed considering the following Argentine regulations:

- Ley 26.529 — Derechos del Paciente, Historia Clínica y Consentimiento Informado.
- Ley 26.742 — Modification of Ley 26.529.
- Ley 26.812 — Specific requirements related to dental clinical records.
- Ley 27.706 — Programa Federal Único de Informatización y Digitalización de las Historias Clínicas de la República Argentina.
- Decreto 393/2023 — Regulatory framework for Ley 27.706.
- Ley 25.326 — Protection of Personal Data.
- Ley 25.506 — Digital Signature.

The final legal applicability of each requirement must be validated with
qualified legal or compliance professionals before production deployment.

---

## 3. Clinical Record

The MedicalRecord is considered a longitudinal clinical record associated
with a patient.

The system must be designed to preserve:

- Integrity.
- Authenticity.
- Confidentiality.
- Traceability.
- Recoverability.
- Long-term preservation.
- Controlled access.

Clinical information must not be silently altered or deleted.

---

## 4. Patient Ownership and Access

The patient is considered the holder of the clinical record.

The architecture should support future functionality allowing patients to:

- Access their clinical information.
- Request a copy of their MedicalRecord.
- Receive information through appropriate secure mechanisms.

---

## 5. Retention

Medical records must be retained for the minimum period established by
applicable legislation.

The current design considers a minimum retention period of ten years from
the last recorded clinical action, subject to legal validation and future
regulatory changes.

---

## 6. Dental Record

Because DentalPro manages dental clinical information, the system should
support a standardized dental record identifying individual dental pieces.

Product scope decision: the MVP includes a simplified Odontogram for
permanent dentition using FDI notation and basic tooth states, without tooth
surface detail. This implementation choice is not itself a legal
requirement. Historical changes must remain traceable as part of clinical
record integrity.

---

## 7. Clinical Traceability

Relevant clinical operations must be auditable.

The system should be able to determine:

- Who performed an operation.
- When it was performed.
- Which clinical information was affected.
- What type of operation was performed.

Clinical modifications must preserve historical traceability.

---

## 8. Informed Consent

The domain model reserves a future Consent concept for recording informed
consents associated with treatments or clinical procedures when applicable.

Consent management is outside the initial MVP by product scope decision.
This scope decision does not determine whether consent is legally required
for a particular treatment or procedure; applicable obligations must be
assessed separately.

---

## 9. Security

Clinical information requires restricted access.

The product security design considers:

- Individual user accounts.
- Role-Based Access Control.
- Backend authorization.
- Secure communication through HTTPS.
- Password hashing.
- Audit logging.
- Backup and recovery mechanisms.

Product scope decision: the MVP includes individual accounts, RBAC, backend
authorization, HTTPS, password hashing and session expiration. 2FA is
future product scope. This MVP boundary is not a statement that 2FA is or
is not legally required.

---

## 10. Interoperability

The architecture should not prevent future interoperability with external
health information systems.

The domain model should remain independent from specific interoperability
protocols.

Future integrations may use healthcare interoperability standards such as
FHIR and standardized clinical terminology such as SNOMED CT.

External interoperability, including FHIR and SNOMED CT, is outside the
initial MVP by product scope decision. This is not a determination of any
legal interoperability obligation.

---

## 11. Architectural Principle

Regulatory requirements should influence the architecture without coupling
the domain model directly to external regulations, infrastructure providers or interoperability technologies.

The system should therefore separate:

- Domain rules.
- Application use cases.
- Infrastructure.
- Security mechanisms.
- Audit mechanisms.
- External interoperability mechanisms.

This allows DentalPro to evolve as regulatory requirements and technical
standards change.
