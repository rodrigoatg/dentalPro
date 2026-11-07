import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminApiService } from './admin-api.service';

describe('AdminApiService', () => {
  let service: AdminApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdminApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the implemented user and role administration endpoints and contracts', () => {
    service.listUsers().subscribe();
    expect(http.expectOne('/api/admin/users').request.method).toBe('GET');
    const create = { userName: 'dentist', email: null, firstName: 'A', lastName: 'B', password: 'secret', roles: ['Dentist'] };
    service.createUser(create).subscribe();
    const createRequest = http.expectOne('/api/admin/users');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(create);
    service.setUserRoles('user-id', ['Dentist', 'Receptionist']).subscribe();
    expect(http.expectOne('/api/admin/users/user-id/roles').request.body).toEqual({ roles: ['Dentist', 'Receptionist'] });
    service.setUserStatus('user-id', false).subscribe();
    expect(http.expectOne('/api/admin/users/user-id/status').request.body).toEqual({ isActive: false });
    service.listRoles().subscribe();
    expect(http.expectOne('/api/admin/roles').request.method).toBe('GET');
    service.getRolePermissions('role-id').subscribe();
    expect(http.expectOne('/api/admin/roles/role-id/permissions').request.method).toBe('GET');
    service.setRolePermissions('role-id', ['patients.view']).subscribe();
    expect(http.expectOne('/api/admin/roles/role-id/permissions').request.body).toEqual({ permissionCodes: ['patients.view'] });
    service.listPermissions().subscribe();
    expect(http.expectOne('/api/admin/permissions').request.method).toBe('GET');
  });
});
