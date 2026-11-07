import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AuthService } from './core/auth/auth.service';
import { App } from './app';

describe('App', () => {
  const authMock = {
    isAuthenticated: signal(true),
    user: signal({ id: 'u1', userName: 'ana', email: null, firstName: 'Ana', lastName: 'Dentist', isActive: true, roles: ['Dentist'], effectivePermissions: ['patients.view'] }),
    hasPermission: (code: string) => code === 'patients.view',
    authorizationMessage: signal(''),
    dismissAuthorizationMessage: () => undefined,
    logout: () => undefined,
  };

  beforeEach(async () => {
    authMock.isAuthenticated.set(true);
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), { provide: AuthService, useValue: authMock }],
    }).compileComponents();
  });

  it('should create the application shell', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the DentalPro identity and main navigation', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('DentalPro');
    expect(compiled.querySelector('.main-nav')?.textContent).toContain('Pacientes');
  });

  it('shows navigation options only when their effective permission is present', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('.main-nav')!;
    expect(nav.textContent).toContain('Pacientes');
    expect(nav.textContent).not.toContain('Administración');
  });
});
