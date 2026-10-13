import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PatientList } from './patient-list';

describe('PatientList', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PatientList],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

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
  });
});
