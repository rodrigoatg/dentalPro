import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Dashboard } from './dashboard';

describe('Dashboard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the patient count returned by the API', async () => {
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/patients').flush([
      {
        id: '1',
        documentType: 'DNI',
        documentNumber: '1',
        firstName: 'Ana',
        lastName: 'Paz',
        dateOfBirth: null,
        phone: null,
        email: null,
        isActive: true,
        createdAt: '2026-10-13T12:00:00Z',
        medicalRecordNumber: 1,
      },
      {
        id: '2',
        documentType: 'DNI',
        documentNumber: '2',
        firstName: 'Sol',
        lastName: 'Luz',
        dateOfBirth: null,
        phone: null,
        email: null,
        isActive: true,
        createdAt: '2026-10-13T12:00:00Z',
        medicalRecordNumber: 2,
      },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('2');
  });
});
