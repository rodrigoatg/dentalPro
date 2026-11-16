import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../core/auth/auth.service';
import { routes } from '../app.routes';

describe('Patient list to detail route', () => {
  let http: HttpTestingController;
  const patientId = '9f2bf832-a604-4495-8896-4f7fb56b9794';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter(routes),
        { provide: AuthService, useValue: { isAuthenticated: () => true, hasPermission: () => true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('navigates using the list PatientId and requests that same GUID from the detail API', async () => {
    const harness = await RouterTestingHarness.create('/patients');
    const list = {
      id: patientId, documentType: 'DNI', documentNumber: '123', firstName: 'Ana', lastName: 'Paz',
      dateOfBirth: null, phone: null, email: null, isActive: true, createdAt: '2026-11-16T10:00:00Z', medicalRecordNumber: 44,
    };
    http.expectOne('/api/patients').flush([list]);
    await harness.fixture.whenStable(); harness.detectChanges();
    const link = harness.routeNativeElement!.querySelector<HTMLAnchorElement>('.patient-detail-link')!;
    expect(link.getAttribute('href')).toBe(`/patients/${patientId}`);
    link.click();
    await harness.fixture.whenStable(); harness.detectChanges();
    const detailRequest = http.expectOne(`/api/patients/${patientId}`);
    detailRequest.flush({ ...list, address: null, updatedAt: '2026-11-16T10:00:00Z' });
    await harness.fixture.whenStable(); harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Paz, Ana');
  });
});
