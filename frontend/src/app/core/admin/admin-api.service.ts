import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateUserRequest, PermissionDefinition, RolePermissionSummary, RoleSummary, UserSummary } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin';

  listUsers(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.base}/users`);
  }

  createUser(request: CreateUserRequest): Observable<UserSummary> {
    return this.http.post<UserSummary>(`${this.base}/users`, request);
  }

  setUserRoles(id: string, roles: string[]): Observable<UserSummary> {
    return this.http.put<UserSummary>(`${this.base}/users/${encodeURIComponent(id)}/roles`, { roles });
  }

  setUserStatus(id: string, isActive: boolean): Observable<UserSummary> {
    return this.http.patch<UserSummary>(`${this.base}/users/${encodeURIComponent(id)}/status`, { isActive });
  }

  listRoles(): Observable<RoleSummary[]> {
    return this.http.get<RoleSummary[]>(`${this.base}/roles`);
  }

  getRolePermissions(id: string): Observable<RolePermissionSummary> {
    return this.http.get<RolePermissionSummary>(`${this.base}/roles/${encodeURIComponent(id)}/permissions`);
  }

  setRolePermissions(id: string, permissionCodes: string[]): Observable<RolePermissionSummary> {
    return this.http.put<RolePermissionSummary>(
      `${this.base}/roles/${encodeURIComponent(id)}/permissions`,
      { permissionCodes },
    );
  }

  listPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${this.base}/permissions`);
  }
}
