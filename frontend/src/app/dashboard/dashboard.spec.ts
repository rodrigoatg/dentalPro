import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { Dashboard } from './dashboard';

describe('Dashboard', () => {
  let http: HttpTestingController;
  let permissions: Set<string>;

  beforeEach(() => {
    permissions = new Set();
    TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: AuthService, useValue: {
          hasPermission: (code: string) => permissions.has(code),
          user: signal({ firstName: 'Ana' }),
        } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows real admin indicators and weekly appointments', async () => {
    permissions = new Set(['users.view', 'patients.view', 'appointments.view', 'appointments.view.all', 'appointments.create']);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();

    http.expectOne('/api/patients/count').flush({ count: 12 });
    http.expectOne('/api/appointments/dentists').flush([
      { id: 'dentist-1', firstName: 'Luis', lastName: 'Paz' },
      { id: 'dentist-2', firstName: 'Eva', lastName: 'Sol' },
    ]);
    http.expectOne('/api/appointments/schedule/settings').flush({
      timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15,
    });
    const list = http.expectOne((request) => request.url === '/api/appointments');
    expect(list.request.params.has('dentistId')).toBe(false);
    const weekStart = mondayUtc(new Date());
    list.flush([
      appointment('a1', weekStart, 'Scheduled'),
      appointment('a2', weekStart, 'Confirmed'),
      appointment('a3', weekStart, 'Cancelled'),
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('12');
    expect(text).toContain('2');
    expect(text).toContain('pendientes');
    expect(text).toContain('confirmados');
    expect(text).toContain('cancelados');
    expect(text).toContain('Actividad reciente de turnos');
  });

  it('shows a receptionist general weekly agenda and does not request admin-only metrics', async () => {
    permissions = new Set(['appointments.view', 'appointments.view.all', 'appointments.create', 'patients.view', 'patients.create']);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/appointments/schedule/settings').flush({
      timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15,
    });
    const list = http.expectOne((request) => request.url === '/api/appointments');
    expect(list.request.params.has('dentistId')).toBe(false);
    const start = mondayUtc(new Date());
    list.flush([appointment('r1', start, 'Scheduled')]);
    await fixture.whenStable();
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Operación de la clínica');
    expect(text).toContain('AGENDA GENERAL');
    expect(text).toContain('Abrir agenda');
    expect(text).toContain('Pacientes');
    expect(http.match('/api/patients/count').length).toBe(0);
    expect(http.match('/api/appointments/dentists').length).toBe(0);
  });

  it('shows only the authenticated dentist scope and allows permitted patient actions', async () => {
    permissions = new Set(['appointments.view', 'clinical.records.view', 'patients.view', 'appointments.confirm', 'appointments.complete']);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/appointments/schedule/settings').flush({
      timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15,
    });
    const list = http.expectOne((request) => request.url === '/api/appointments');
    expect(list.request.params.has('dentistId')).toBe(false);
    const today = new Date().toISOString().slice(0, 10);
    list.flush([appointment('d1', today, 'Scheduled')]);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('AGENDA DEL ODONTÓLOGO');
    expect(text).toContain('Ficha del paciente');
    expect(text).toContain('Historia clínica');
    const confirm = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find((button) => button.textContent?.trim() === 'Confirmar');
    expect(confirm).toBeTruthy();
    confirm!.click();
    fixture.detectChanges();
    http.expectOne({ method: 'POST', url: '/api/appointments/d1/confirm' }).flush(appointment('d1', today, 'Confirmed'));
    http.expectOne((request) => request.url === '/api/appointments').flush([appointment('d1', today, 'Confirmed')]);
    await fixture.whenStable();
  });
});

function mondayUtc(now: Date): string {
  const date = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  date.setUTCDate(date.getUTCDate() - ((date.getUTCDay() + 6) % 7));
  return date.toISOString().slice(0, 10);
}

function appointment(id: string, date: string, status: string) {
  return {
    appointmentId: id, patientId: `patient-${id}`, patientFirstName: 'Ana', patientLastName: 'Paz',
    dentistId: `dentist-${id}`, dentistFirstName: 'Luis', dentistLastName: 'Sol',
    startDateTime: `${date}T15:00:00Z`, endDateTime: `${date}T15:30:00Z`, durationMinutes: 30,
    status, notes: null, rescheduledFromAppointmentId: null,
    createdAt: `${date}T12:00:00Z`, updatedAt: `${date}T12:00:00Z`,
  };
}
