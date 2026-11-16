import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreatePatientRequest, CreatedPatient, PatientListItem } from './patient.models';

@Injectable({ providedIn: 'root' })
export class PatientsApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/patients';

  list(search?: string): Observable<PatientListItem[]> {
    const normalizedSearch = search?.trim().replace(/\s+/g, ' ');
    const params = normalizedSearch ? new HttpParams().set('search', normalizedSearch) : undefined;
    return this.http.get<PatientListItem[]>(this.endpoint, { params });
  }

  get(id: string): Observable<CreatedPatient> {
    return this.http.get<CreatedPatient>(`${this.endpoint}/${encodeURIComponent(id)}`);
  }

  update(id: string, request: CreatePatientRequest): Observable<void> {
    return this.http.put<void>(`${this.endpoint}/${encodeURIComponent(id)}`, request);
  }

  create(request: CreatePatientRequest): Observable<CreatedPatient> {
    return this.http.post<CreatedPatient>(this.endpoint, request);
  }
}
