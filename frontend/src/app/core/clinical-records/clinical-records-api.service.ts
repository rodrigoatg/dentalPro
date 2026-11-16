import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ClinicalRecord, CreateClinicalEntryRequest } from './clinical-record.models';

@Injectable({ providedIn: 'root' })
export class ClinicalRecordsApiService {
  private readonly http = inject(HttpClient);
  private endpoint(patientId: string): string { return `/api/patients/${encodeURIComponent(patientId)}/medical-record`; }
  get(patientId: string): Observable<ClinicalRecord> { return this.http.get<ClinicalRecord>(this.endpoint(patientId)); }
  createEntry(patientId: string, request: CreateClinicalEntryRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.endpoint(patientId)}/entries`, request);
  }
}
