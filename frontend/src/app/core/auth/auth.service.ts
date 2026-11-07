import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, switchMap, tap, catchError, throwError } from 'rxjs';
import { CurrentUser, LoginRequest, LoginResponse } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly token = signal<string | null>(null);
  private readonly currentUser = signal<CurrentUser | null>(null);
  private readonly forbidden = signal('');

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null && this.token() !== null);
  readonly authorizationMessage = this.forbidden.asReadonly();

  accessToken(): string | null {
    return this.token();
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.http.post<LoginResponse>('/api/auth/login', request).pipe(
      tap((response) => this.token.set(response.accessToken)),
      switchMap(() => this.http.get<CurrentUser>('/api/auth/me')),
      tap((user) => {
        this.currentUser.set(user);
        this.forbidden.set('');
      }),
      catchError((error: unknown) => {
        this.clearSession();
        return throwError(() => error);
      }),
    );
  }

  refreshCurrentUser(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>('/api/auth/me').pipe(
      tap((user) => this.currentUser.set(user)),
      catchError((error: unknown) => {
        this.clearSession();
        return throwError(() => error);
      }),
    );
  }

  hasPermission(permissionCode: string): boolean {
    return this.currentUser()?.effectivePermissions.includes(permissionCode) ?? false;
  }

  logout(): void {
    this.clearSession();
  }

  clearSession(): void {
    this.token.set(null);
    this.currentUser.set(null);
    this.forbidden.set('');
  }

  reportForbidden(): void {
    this.forbidden.set('No tenés permiso para realizar esta acción.');
  }

  dismissAuthorizationMessage(): void {
    this.forbidden.set('');
  }
}
