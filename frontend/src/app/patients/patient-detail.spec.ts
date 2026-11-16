import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { PatientDetail } from './patient-detail';

describe('PatientDetail', () => {
  let http: HttpTestingController;
  let permissions: string[];

  const profile = {
    id: 'patient-1', documentType: 'DNI', documentNumber: '123', firstName: 'Ana', lastName: 'Paz',
    dateOfBirth: null, phone: null, email: null, address: null, isActive: true,
    createdAt: '2026-11-16T10:00:00Z', updatedAt: '2026-11-16T10:00:00Z', medicalRecordNumber: 12,
  };

  beforeEach(() => {
    permissions = ['patients.view'];
    TestBed.configureTestingModule({
      imports: [PatientDetail],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'patient-1' } } } },
        { provide: AuthService, useValue: { hasPermission: (code: string) => permissions.includes(code) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the edit action disabled with an explanation when the permission is absent', async () => {
    const fixture = TestBed.createComponent(PatientDetail); fixture.detectChanges();
    http.expectOne('/api/patients/patient-1').flush(profile);
    await fixture.whenStable(); fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    const button = page.querySelector<HTMLButtonElement>('.edit-action button');
    expect(button?.textContent).toContain('Editar datos');
    expect(button?.disabled).toBe(true);
    expect(page.textContent).toContain('patients.update');
    http.expectNone((request) => request.method === 'PUT');
  });

  it('opens the form and sends personal data to the update endpoint when authorized', async () => {
    permissions = ['patients.view', 'patients.update'];
    const fixture = TestBed.createComponent(PatientDetail); fixture.detectChanges();
    http.expectOne('/api/patients/patient-1').flush(profile);
    await fixture.whenStable(); fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    page.querySelector<HTMLButtonElement>('.edit-action button')!.click(); fixture.detectChanges();
    const firstName = page.querySelector<HTMLInputElement>('[formControlName="firstName"]')!;
    firstName.value = 'Ana María'; firstName.dispatchEvent(new Event('input')); fixture.detectChanges();
    page.querySelector<HTMLButtonElement>('.actions button[type="submit"]')!.click();
    const request = http.expectOne('/api/patients/patient-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.firstName).toBe('Ana María');
    request.flush(null);
    http.expectOne('/api/patients/patient-1').flush({ ...profile, firstName: 'Ana María' });
    await fixture.whenStable(); fixture.detectChanges();
    expect(page.textContent).toContain('Datos personales actualizados.');
  });
});
