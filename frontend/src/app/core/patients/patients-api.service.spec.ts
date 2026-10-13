import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PatientsApiService } from './patients-api.service';

describe('PatientsApiService', () => {
  let service: PatientsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PatientsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the patient list from the API', () => {
    service.list().subscribe((patients) => expect(patients).toHaveLength(0));
    const request = http.expectOne('/api/patients');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('posts the patient contract to the API', () => {
    const input = {
      documentType: 'DNI',
      documentNumber: '123',
      firstName: 'Ana',
      lastName: 'Paz',
      dateOfBirth: null,
      phone: null,
      email: null,
      address: null,
    };
    service.create(input).subscribe((created) => expect(created.medicalRecordNumber).toBe(1));
    const request = http.expectOne('/api/patients');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(input);
    request.flush({
      ...input,
      id: 'patient-id',
      isActive: true,
      createdAt: '2026-10-13T12:00:00Z',
      medicalRecordNumber: 1,
    });
  });
});
