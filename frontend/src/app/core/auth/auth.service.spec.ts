import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores a successful login token only in memory and loads the current user', () => {
    auth.login({ userName: 'dentist', password: 'secret' }).subscribe((user) => {
      expect(user.userName).toBe('dentist');
    });
    const login = http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    login.flush({ userId: 'u1', userName: 'dentist', roles: ['Dentist'], accessToken: 'memory-jwt', expiresAt: '2026-11-04T12:15:00Z' });
    const me = http.expectOne('/api/auth/me');
    expect(me.request.method).toBe('GET');
    me.flush({
      id: 'u1', userName: 'dentist', email: null, firstName: 'Ana', lastName: 'Dentist', isActive: true,
      roles: ['Dentist'], effectivePermissions: ['patients.view'],
    });

    expect(auth.accessToken()).toBe('memory-jwt');
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.hasPermission('patients.view')).toBe(true);
    expect(auth.hasPermission('users.view')).toBe(false);
  });

  it('clears the session after invalid credentials', () => {
    let failed = false;
    auth.login({ userName: 'wrong', password: 'wrong' }).subscribe({ error: () => { failed = true; } });
    http.expectOne('/api/auth/login').flush({ title: 'Invalid username or password.' }, { status: 401, statusText: 'Unauthorized' });
    expect(failed).toBe(true);
    expect(auth.accessToken()).toBeNull();
    expect(auth.user()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('clears token and user on logout', () => {
    auth.logout();
    expect(auth.accessToken()).toBeNull();
    expect(auth.user()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
