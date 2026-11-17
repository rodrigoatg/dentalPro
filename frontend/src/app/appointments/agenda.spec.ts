import { of } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../core/auth/auth.service';
import { AppointmentsApiService } from '../core/appointments/appointments-api.service';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { PatientListItem } from '../core/patients/patient.models';
import { Agenda } from './agenda';

describe('Agenda', () => {
  const permissions = new Set<string>();
  const authMock = { hasPermission: (permission: string) => permissions.has(permission) };
  let patientSearchCalls: (string | undefined)[];
  let patientDirectory: PatientListItem[];
  const apiMock = {
    list: () => of([]), dentists: () => of([]), scheduleSettings: () => of({ timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15 }),
    schedule: () => of({ id: 's1', timeZoneId: 'UTC', defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15, weeklyIntervals: [], dateExceptions: [] }),
    availability: () => of({ availableIntervals: [], defaultAppointmentDurationMinutes: 30, startIncrementMinutes: 15 }),
  };
  const patientsMock = {
    list: (search?: string) => {
      patientSearchCalls.push(search);
      return of(search ? patientDirectory : []);
    },
  };

  beforeEach(async () => {
    permissions.clear();
    patientSearchCalls = [];
    patientDirectory = [];
    await TestBed.configureTestingModule({
      imports: [Agenda],
      providers: [
        provideRouter([{ path: 'appointments', component: Agenda }]),
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

  it('opens the existing creation dialog from the dashboard deep link with the selected date', async () => {
    permissions.add('appointments.view');
    permissions.add('appointments.create');
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/appointments?create=1&date=2026-11-20', Agenda);
    expect(harness.routeNativeElement?.textContent).toContain('Nuevo turno');
    expect(harness.routeNativeElement?.querySelector<HTMLInputElement>('input[name="appointmentDate"]')?.value)
      .toBe('2026-11-20');
  });

  it('searches patients on demand, retains the selected ID, and allows replacing the selection', async () => {
    permissions.add('appointments.view');
    permissions.add('appointments.create');
    permissions.add('patients.view');
    const firstPatient: PatientListItem = {
      id: 'patient-1', documentType: 'DNI', documentNumber: '12345', firstName: 'Ana', lastName: 'Paz',
      dateOfBirth: null, phone: null, email: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', medicalRecordNumber: 1,
    };
    const secondPatient: PatientListItem = { ...firstPatient, id: 'patient-2', documentNumber: '67890', firstName: 'Sol', lastName: 'Luz' };
    patientDirectory = [firstPatient];
    const fixture = TestBed.createComponent(Agenda);
    fixture.detectChanges();
    expect(patientSearchCalls).toEqual([]);

    const component = fixture.componentInstance as unknown as {
      openCreate: () => void;
      searchPatients: (value: string) => void;
      selectedPatientId: () => string;
    };
    component.openCreate();
    fixture.detectChanges();
    component.searchPatients('  Ana   Paz ');
    await new Promise((resolve) => setTimeout(resolve, 275));
    fixture.detectChanges();
    expect(patientSearchCalls).toEqual(['Ana Paz']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Paz, Ana');

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[role="option"]')!.click();
    fixture.detectChanges();
    expect(component.selectedPatientId()).toBe('patient-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('DNI 12345');

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.selected-patient button')!.click();
    patientDirectory = [secondPatient];
    fixture.detectChanges();
    component.searchPatients('678');
    await new Promise((resolve) => setTimeout(resolve, 275));
    fixture.detectChanges();
    expect(patientSearchCalls).toEqual(['Ana Paz', '678']);
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[role="option"]')!.click();
    expect(component.selectedPatientId()).toBe('patient-2');
    expect(patientSearchCalls).not.toContain(undefined);
  });
});
