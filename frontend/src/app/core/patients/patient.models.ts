export interface CreatePatientRequest {
  documentType: string;
  documentNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
}

export interface PatientListItem {
  id: string;
  documentType: string;
  documentNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string | null;
  phone: string | null;
  email: string | null;
  isActive: boolean;
  createdAt: string;
  medicalRecordNumber: number;
}

export interface CreatedPatient extends PatientListItem {
  address: string | null;
}
