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
    http.expectOne('/api/appointments/dentists').flush([
      { id: 'dentist-1', firstName: 'Luis', lastName: 'Paz' },
    ]);
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
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Odontólogo');
  });

  it('combines dentist and status filters, changes period without losing date, and exposes create only with permission', async () => {
    permissions = new Set(['appointments.view', 'appointments.view.all', 'appointments.create']);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/appointments/dentists').flush([
      { id: 'dentist-1', firstName: 'Luis', lastName: 'Paz' },
      { id: 'dentist-2', firstName: 'Eva', lastName: 'Sol' },
    ]);
    http.expectOne('/api/appointments/schedule/settings').flush({
      timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15,
    });
    http.expectOne((request) => request.url === '/api/appointments').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const createLink = Array.from(element.querySelectorAll('a')).find((anchor) => anchor.textContent?.includes('Nuevo turno'));
    expect(createLink).toBeTruthy();
    expect(createLink!.getAttribute('href')).toContain('create=1');
    const selects = element.querySelectorAll('select');
    const dentistSelect = selects[0];
    dentistSelect.value = 'dentist-2';
    dentistSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const dentistRequest = http.expectOne((request) => request.url === '/api/appointments');
    expect(dentistRequest.request.params.get('dentistId')).toBe('dentist-2');
    const selectedDay = new Date().toISOString().slice(0, 10);
    const futureStart = new Date(Date.now() + 30 * 60_000).toISOString();
    const futureConfirmed = { ...appointment('f2', selectedDay, 'Confirmed'), startDateTime: futureStart };
    dentistRequest.flush([
      appointment('f1', selectedDay, 'Scheduled'), futureConfirmed,
      appointment('f3', selectedDay, 'Completed'), appointment('f4', selectedDay, 'Cancelled'),
      appointment('f5', selectedDay, 'Rescheduled'),
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
    for (const stateClass of ['status-scheduled', 'status-confirmed', 'status-completed', 'status-cancelled', 'status-rescheduled']) {
      expect(element.querySelector(`.appointment-row.${stateClass}`)).toBeTruthy();
    }

    const statusSelect = selects[1];
    statusSelect.value = 'Confirmed';
    statusSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(element.querySelectorAll('.appointment-row')).toHaveLength(1);
    expect(element.querySelector('.appointment-row.status-confirmed')).toBeTruthy();
    expect(element.querySelector('.upcoming-row')).toBeTruthy();

    const viewSelect = selects[2];
    viewSelect.value = 'day';
    viewSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const dayRequest = http.expectOne((request) => request.url === '/api/appointments');
    expect(dayRequest.request.params.get('dentistId')).toBe('dentist-2');
    expect(Date.parse(dayRequest.request.params.get('to')!) - Date.parse(dayRequest.request.params.get('from')!)).toBeLessThan(
      Date.parse(dentistRequest.request.params.get('to')!) - Date.parse(dentistRequest.request.params.get('from')!),
    );
    dayRequest.flush([]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.querySelector('.week-grid.day-view')).toBeTruthy();
    const dayNext = element.querySelector<HTMLButtonElement>('button[aria-label="Día siguiente"]');
    expect(dayNext).toBeTruthy();
    dayNext!.click();
    fixture.detectChanges();
    const nextDayRequest = http.expectOne((request) => request.url === '/api/appointments');
    expect(nextDayRequest.request.params.get('dentistId')).toBe('dentist-2');
    expect(Date.parse(nextDayRequest.request.params.get('from')!) - Date.parse(dayRequest.request.params.get('from')!))
      .toBe(86_400_000);
    nextDayRequest.flush([]);
    await fixture.whenStable();

  });

  it('does not expose the new appointment action without appointments.create', async () => {
    permissions = new Set(['appointments.view', 'appointments.view.all']);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/appointments/schedule/settings').flush({
      timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15,
    });
    http.expectOne((request) => request.url === '/api/appointments').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(Array.from(element.querySelectorAll('a')).some((anchor) => anchor.textContent?.includes('Nuevo turno'))).toBe(false);
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
