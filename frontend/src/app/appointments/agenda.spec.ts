import { of } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { AppointmentsApiService } from '../core/appointments/appointments-api.service';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { Agenda } from './agenda';

describe('Agenda', () => {
  const permissions = new Set<string>();
  const authMock = { hasPermission: (permission: string) => permissions.has(permission) };
  const apiMock = {
    list: () => of([]), dentists: () => of([]), scheduleSettings: () => of({ timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15 }),
    schedule: () => of({ id: 's1', timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15, weeklyIntervals: [], dateExceptions: [] }),
    availability: () => of({ availableIntervals: [], defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15 }),
  };
  const patientsMock = { list: () => of([]) };

  beforeEach(async () => {
    permissions.clear();
    await TestBed.configureTestingModule({
      imports: [Agenda],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authMock },
        { provide: AppointmentsApiService, useValue: apiMock },
        { provide: PatientsApiService, useValue: patientsMock },
      ],
    }).compileComponents();
  });

  it('hides creation and schedule administration when effective permissions are absent', () => {
    permissions.add('appointments.view');
    const fixture = TestBed.createComponent(Agenda);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Nuevo turno');
    expect(text).not.toContain('Configurar horarios');
  });

  it('shows actions only for effective create and schedule management permissions', () => {
    permissions.add('appointments.view');
    permissions.add('appointments.create');
    permissions.add('appointments.schedule.manage');
    const fixture = TestBed.createComponent(Agenda);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Nuevo turno');
    expect(text).toContain('Configurar horarios');
  });
});
