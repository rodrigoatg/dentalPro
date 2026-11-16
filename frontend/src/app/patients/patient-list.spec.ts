import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PatientList } from './patient-list';
import { AuthService } from '../core/auth/auth.service';

describe('PatientList', () => {
  let http: HttpTestingController;
  let permissions: string[];

  beforeEach(() => {
    permissions = ['patients.create'];
    TestBed.configureTestingModule({
      imports: [PatientList],
      providers: [
        provideRouter([]),
        provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { hasPermission: (code: string) => permissions.includes(code) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('allows viewing patients but prevents creation without patients.create', async () => {
    permissions = ['patients.view'];
    const fixture = TestBed.createComponent(PatientList);
    fixture.detectChanges();

    const listRequest = http.expectOne('/api/patients');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.page-heading button')).toBeNull();
    expect(page.querySelector('.empty-state button')).toBeNull();
    expect(page.querySelector('[role="dialog"]')).toBeNull();

    const component = fixture.componentInstance as unknown as {
      openForm: () => void;
      submit: () => void;
      patientForm: { patchValue: (value: Record<string, string>) => void };
    };
    component.openForm();
    fixture.detectChanges();
    expect(page.querySelector('[role="dialog"]')).toBeNull();

    component.patientForm.patchValue({
      documentType: 'DNI',
      documentNumber: '456',
      firstName: 'Luc\u00EDa',
      lastName: 'Mora',
    });
    component.submit();
    http.expectNone((request) => request.method === 'POST');
  });

  it('creates a patient, refreshes the list, and shows its medical record number', async () => {
    const fixture = TestBed.createComponent(PatientList);
    fixture.detectChanges();
    http.expectOne('/api/patients').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    page.querySelector<HTMLButtonElement>('.page-heading button')!.click();
    fixture.detectChanges();
    const input = (name: string) =>
      page.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
    for (const [name, value] of Object.entries({
      documentType: 'DNI',
      documentNumber: '456',
      firstName: 'Luc\u00EDa',
      lastName: 'Mora',
    })) {
      input(name).value = value;
      input(name).dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
    page.querySelector<HTMLButtonElement>('.modal-actions [type="submit"]')!.click();

    const createRequest = http.expectOne('/api/patients');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush({
      id: 'patient-1',
      documentType: 'DNI',
      documentNumber: '456',
      firstName: 'Luc\u00EDa',
      lastName: 'Mora',
      dateOfBirth: null,
      phone: null,
      email: null,
      address: null,
      isActive: true,
      createdAt: '2026-10-13T12:00:00Z',
      medicalRecordNumber: 42,
    });

    http.expectOne('/api/patients').flush([
      {
        id: 'patient-1',
        documentType: 'DNI',
        documentNumber: '456',
        firstName: 'Luc\u00EDa',
        lastName: 'Mora',
        dateOfBirth: null,
        phone: null,
        email: null,
        isActive: true,
        createdAt: '2026-10-13T12:00:00Z',
        medicalRecordNumber: 42,
      },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    const content = page.textContent ?? '';
    expect(content).toContain('HC-000042');
    expect(content).toContain('Mora, Luc\u00EDa');
    expect(page.querySelector('.patient-detail-link')?.textContent).toContain('Abrir ficha del paciente');
  });

  it('shows a searchable list and filters through the API service', async () => {
    permissions = ['patients.view'];
    const fixture = TestBed.createComponent(PatientList);
    fixture.detectChanges();
    const patient = {
      id: 'patient-1', documentType: 'DNI', documentNumber: '456', firstName: 'Lucía', lastName: 'Mora',
      dateOfBirth: null, phone: null, email: null, isActive: true, createdAt: '2026-10-13T12:00:00Z', medicalRecordNumber: 42,
    };
    http.expectOne('/api/patients').flush([patient]);
    await fixture.whenStable(); fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    const search = page.querySelector<HTMLInputElement>('.patient-search input');
    expect(search).not.toBeNull();
    search!.value = '  456   '; search!.dispatchEvent(new Event('input')); fixture.detectChanges();
    const filteredRequest = http.expectOne((item) => item.url === '/api/patients' && item.params.get('search') === '456');
    expect(filteredRequest.request.method).toBe('GET');
    search!.value = ''; search!.dispatchEvent(new Event('input')); fixture.detectChanges();
    const fullListRequest = http.expectOne('/api/patients');
    fullListRequest.flush([patient, { ...patient, id: 'patient-2', documentNumber: '789', firstName: 'Sofía' }]);
    await fixture.whenStable(); fixture.detectChanges();
    filteredRequest.flush([patient]);
    await fixture.whenStable(); fixture.detectChanges();
    expect(page.querySelectorAll('tbody tr').length).toBe(2);
    expect(page.querySelector<HTMLInputElement>('.patient-search input')?.value).toBe('');
    expect(page.querySelector<HTMLAnchorElement>('.patient-detail-link')?.getAttribute('href')).toBe('/patients/patient-1');
  });
});
