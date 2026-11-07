import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authenticatedGuard, permissionGuard } from './auth.guards';

describe('route guards', () => {
  const route = { data: {} } as never;
  const state = { url: '/patients' } as never;

  it('redirects unauthenticated users to login with the requested URL', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isAuthenticated: () => false } }],
    });
    const result = TestBed.runInInjectionContext(() => authenticatedGuard(route, state));
    expect(TestBed.inject(Router).serializeUrl(result as never)).toContain('returnUrl=%2Fpatients');
  });

  it('allows a route only when the current user has its permission', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: {
        isAuthenticated: () => true,
        hasPermission: (code: string) => code === 'patients.view',
      } }],
    });
    const authorized = TestBed.runInInjectionContext(() => permissionGuard(
      { data: { permission: 'patients.view' } } as never, state,
    ));
    const denied = TestBed.runInInjectionContext(() => permissionGuard(
      { data: { permission: 'users.view' } } as never, state,
    ));
    expect(authorized).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(denied as never)).toBe('/forbidden');
  });
});
