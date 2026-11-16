export interface ClinicalTreatmentTooth {
  toothNumber: number; status: string; completedAt: string | null; notes: string | null; resultingToothState: string | null;
}
export interface ClinicalTreatment {
  id: string; dentistId: string; dentistFirstName: string; dentistLastName: string; type: string;
  occurredAt: string; description: string; priority: string; status: string; cost: number | null;
  teeth: ClinicalTreatmentTooth[];
}
export interface ClinicalEntryVersion {
  versionNumber: number; dentistId: string; appointmentId: string | null; type: string;
  occurredAt: string; diagnosis: string | null; notes: string; treatmentIds: string[];
  createdAt: string; createdBy: string; createdByFirstName: string; createdByLastName: string;
  changeReason: string | null;
}
export interface ClinicalEntry {
  id: string; type: string; dentistId: string; dentistFirstName: string; dentistLastName: string;
  appointmentId: string | null; occurredAt: string; diagnosis: string | null; notes: string;
  versions: ClinicalEntryVersion[]; treatments: ClinicalTreatment[];
}
export interface ClinicalRecord {
  id: string; patientId: string; patientFirstName: string; patientLastName: string;
  medicalRecordNumber: number; startedAt: string;
  lastClinicalActivityAt: string | null; entries: ClinicalEntry[];
}
export interface CreateClinicalEntryRequest {
  type: string; occurredAt: string; notes: string; diagnosis: string | null; appointmentId: string | null;
  treatments: { type: string; description: string; status: string; toothNumbers: number[]; completedToothNumbers: number[]; priority: string; cost: number | null }[];
}
