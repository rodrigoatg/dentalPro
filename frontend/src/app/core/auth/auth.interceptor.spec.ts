import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';
import { vi } from 'vitest';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let auth: AuthService;
  const routerStub = { url: '/private', navigate: vi.fn().mockResolvedValue(true) };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(),
        { provide: Router, useValue: routerStub },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('adds the in-memory bearer token and excludes the login request', () => {
    auth.login({ userName: 'user', password: 'pass' }).subscribe();
    http.expectOne('/api/auth/login').flush({ userId: 'u1', userName: 'user', roles: [], accessToken: 'jwt-value', expiresAt: '2026-11-04T12:15:00Z' });
    http.expectOne('/api/auth/me').flush({ id: 'u1', userName: 'user', email: null, firstName: 'U', lastName: 'Ser', isActive: true, roles: [], effectivePermissions: [] });

    client.get('/api/admin/users').subscribe();
    expect(http.expectOne('/api/admin/users').request.headers.get('Authorization')).toBe('Bearer jwt-value');
    client.post('/api/auth/login', {}).subscribe();
    expect(http.expectOne('/api/auth/login').request.headers.has('Authorization')).toBe(false);
  });

  it('clears the session and redirects after a non-login 401', async () => {
    await establishSession(client, http, auth);
    routerStub.navigate.mockClear();
    let failure: unknown;
    client.get('/api/auth/me').subscribe({ error: (error: unknown) => { failure = error; } });
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(failure).toBeInstanceOf(HttpErrorResponse);
    expect(auth.isAuthenticated()).toBe(false);
    expect(routerStub.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/private' } });
  });

  it('surfaces forbidden responses without clearing the session', async () => {
    await establishSession(client, http, auth);
    client.get('/api/admin/users').subscribe({ error: () => undefined });
    http.expectOne('/api/admin/users').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.authorizationMessage()).toContain('No tenés permiso');
  });
});

async function establishSession(client: HttpClient, http: HttpTestingController, auth: AuthService): Promise<void> {
  auth.login({ userName: 'user', password: 'pass' }).subscribe();
  http.expectOne('/api/auth/login').flush({ userId: 'u1', userName: 'user', roles: [], accessToken: 'jwt-value', expiresAt: '2026-11-04T12:15:00Z' });
  http.expectOne('/api/auth/me').flush({ id: 'u1', userName: 'user', email: null, firstName: 'U', lastName: 'Ser', isActive: true, roles: [], effectivePermissions: [] });
  await Promise.resolve();
}
