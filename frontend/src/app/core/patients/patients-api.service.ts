import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreatePatientRequest, CreatedPatient, PatientListItem } from './patient.models';

@Injectable({ providedIn: 'root' })
export class PatientsApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/patients';

  list(): Observable<PatientListItem[]> {
    return this.http.get<PatientListItem[]>(this.endpoint);
  }

  create(request: CreatePatientRequest): Observable<CreatedPatient> {
    return this.http.post<CreatedPatient>(this.endpoint, request);
  }
}
