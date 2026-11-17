import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Appointment, AppointmentRequest, Availability, ClinicDateException, ClinicSchedule,
  ClinicScheduleSettings, ConfigureClinicSchedule, DentistOption, RescheduleRequest,
} from './appointment.models';

@Injectable({ providedIn: 'root' })
export class AppointmentsApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/appointments';

  list(from: string, to: string, dentistId?: string): Observable<Appointment[]> {
    let params = new HttpParams().set('from', from).set('to', to);
    if (dentistId) params = params.set('dentistId', dentistId);
    return this.http.get<Appointment[]>(this.endpoint, { params });
  }
  availability(from: string, to: string, dentistId: string, patientId?: string): Observable<Availability> {
    let params = new HttpParams().set('from', from).set('to', to).set('dentistId', dentistId);
    if (patientId) params = params.set('patientId', patientId);
    return this.http.get<Availability>(`${this.endpoint}/availability`, { params });
  }
  dentists(): Observable<DentistOption[]> { return this.http.get<DentistOption[]>(`${this.endpoint}/dentists`); }
  create(request: AppointmentRequest): Observable<Appointment> { return this.http.post<Appointment>(this.endpoint, request); }
  reschedule(id: string, request: RescheduleRequest): Observable<Appointment> { return this.http.post<Appointment>(`${this.endpoint}/${id}/reschedule`, request); }
  cancel(id: string): Observable<Appointment> { return this.http.post<Appointment>(`${this.endpoint}/${id}/cancel`, {}); }
  confirm(id: string): Observable<Appointment> { return this.http.post<Appointment>(`${this.endpoint}/${id}/confirm`, {}); }
  complete(id: string): Observable<Appointment> { return this.http.post<Appointment>(`${this.endpoint}/${id}/complete`, {}); }

  scheduleSettings(): Observable<ClinicScheduleSettings> { return this.http.get<ClinicScheduleSettings>(`${this.endpoint}/schedule/settings`); }
  schedule(): Observable<ClinicSchedule> { return this.http.get<ClinicSchedule>(`${this.endpoint}/schedule`); }
  createSchedule(request: ConfigureClinicSchedule): Observable<ClinicSchedule> { return this.http.post<ClinicSchedule>(`${this.endpoint}/schedule`, request); }
  updateSchedule(request: ConfigureClinicSchedule): Observable<ClinicSchedule> { return this.http.put<ClinicSchedule>(`${this.endpoint}/schedule`, request); }
  setException(exception: ClinicDateException): Observable<ClinicSchedule> { return this.http.put<ClinicSchedule>(`${this.endpoint}/schedule/exceptions/${exception.date}`, exception); }
  removeException(date: string): Observable<ClinicSchedule> { return this.http.delete<ClinicSchedule>(`${this.endpoint}/schedule/exceptions/${date}`); }
}
