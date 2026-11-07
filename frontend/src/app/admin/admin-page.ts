import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../core/auth/auth.service';
import { AdminApiService } from '../core/admin/admin-api.service';
import { CreateUserRequest, PermissionDefinition, RolePermissionSummary, RoleSummary, UserSummary } from '../core/admin/admin.models';

type AdminTab = 'users' | 'roles' | 'permissions';

@Component({
  selector: 'app-admin-page',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.css',
})
export class AdminPage implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly activeTab = signal<AdminTab>('users');
  protected readonly users = signal<UserSummary[]>([]);
  protected readonly roles = signal<RoleSummary[]>([]);
  protected readonly permissions = signal<PermissionDefinition[]>([]);
  protected readonly rolePermissions = signal<RolePermissionSummary | null>(null);
  protected readonly selectedRoleId = signal('');
  protected readonly selectedPermissionCodes = signal<string[]>([]);
  protected readonly selectedCreateRoles = signal<string[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly userFormVisible = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly busyUserIds = signal<ReadonlySet<string>>(new Set());
  protected readonly createForm = this.formBuilder.nonNullable.group({
    userName: ['', Validators.required],
    email: [''],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected get canViewUsers(): boolean { return this.auth.hasPermission('users.view'); }
  protected get canCreateUsers(): boolean { return this.auth.hasPermission('users.create'); }
  protected get canManageUserRoles(): boolean { return this.auth.hasPermission('users.roles.manage'); }
  protected get canManageUserStatus(): boolean { return this.auth.hasPermission('users.status.manage'); }
  protected get canViewRoles(): boolean { return this.auth.hasPermission('roles.view'); }
  protected get canManageRolePermissions(): boolean { return this.auth.hasPermission('roles.permissions.manage'); }
  protected get canViewPermissions(): boolean { return this.auth.hasPermission('permissions.view'); }

  ngOnInit(): void {
    if (this.canViewUsers) {
      this.activeTab.set('users');
      this.loadUsers();
    } else if (this.canViewRoles) {
      this.activeTab.set('roles');
    } else {
      this.activeTab.set('permissions');
    }
    if (this.canViewRoles) this.loadRoles();
    if (this.canViewPermissions) this.loadPermissions();
  }

  protected selectTab(tab: AdminTab): void {
    this.errorMessage.set('');
    this.successMessage.set('');
    this.activeTab.set(tab);
    if (tab === 'users' && this.users().length === 0) this.loadUsers();
  }

  protected loadUsers(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.listUsers().subscribe({
      next: (users) => { this.users.set(users); this.loading.set(false); },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.loading.set(false); },
    });
  }

  protected openCreateForm(): void {
    this.createForm.reset();
    this.selectedCreateRoles.set([]);
    this.errorMessage.set('');
    this.userFormVisible.set(true);
  }

  protected toggleCreateRole(roleName: string, checked: boolean): void {
    this.selectedCreateRoles.update((current) => checked
      ? [...new Set([...current, roleName])]
      : current.filter((role) => role !== roleName));
  }

  protected createUser(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const values = this.createForm.getRawValue();
    const request: CreateUserRequest = {
      ...values,
      email: values.email.trim() || null,
      roles: this.selectedCreateRoles(),
    };
    this.saving.set(true);
    this.errorMessage.set('');
    this.api.createUser(request).subscribe({
      next: (user) => {
        this.users.update((current) => [user, ...current]);
        this.userFormVisible.set(false);
        this.saving.set(false);
        this.successMessage.set(`Se creó la cuenta ${user.userName}.`);
      },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.saving.set(false); },
    });
  }

  protected toggleUserRole(user: UserSummary, roleName: string, checked: boolean): void {
    const roles = checked
      ? [...new Set([...user.roles, roleName])]
      : user.roles.filter((role) => role !== roleName);
    this.setUserBusy(user.id, true);
    this.errorMessage.set('');
    this.api.setUserRoles(user.id, roles).subscribe({
      next: (updated) => { this.replaceUser(updated); this.setUserBusy(user.id, false); },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.setUserBusy(user.id, false); this.loadUsers(); },
    });
  }

  protected toggleUserStatus(user: UserSummary): void {
    this.setUserBusy(user.id, true);
    this.errorMessage.set('');
    this.api.setUserStatus(user.id, !user.isActive).subscribe({
      next: (updated) => { this.replaceUser(updated); this.setUserBusy(user.id, false); },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.setUserBusy(user.id, false); this.loadUsers(); },
    });
  }

  protected selectRole(roleId: string): void {
    this.selectedRoleId.set(roleId);
    this.rolePermissions.set(null);
    this.selectedPermissionCodes.set([]);
    this.errorMessage.set('');
    if (!roleId) return;
    this.loading.set(true);
    this.api.getRolePermissions(roleId).subscribe({
      next: (result) => {
        this.rolePermissions.set(result);
        this.selectedPermissionCodes.set([...result.assignedCodes]);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.loading.set(false); },
    });
  }

  protected togglePermission(permission: PermissionDefinition, checked: boolean): void {
    this.selectedPermissionCodes.update((current) => checked
      ? [...new Set([...current, permission.code])]
      : current.filter((code) => code !== permission.code));
  }

  protected saveRolePermissions(): void {
    const roleId = this.selectedRoleId();
    if (!roleId) return;
    this.saving.set(true);
    this.errorMessage.set('');
    this.api.setRolePermissions(roleId, this.selectedPermissionCodes()).subscribe({
      next: (result) => {
        this.rolePermissions.set(result);
        this.selectedPermissionCodes.set([...result.assignedCodes]);
        this.saving.set(false);
        this.successMessage.set(`Se actualizaron los permisos de ${result.roleName}.`);
      },
      error: (error: HttpErrorResponse) => { this.errorMessage.set(this.apiError(error)); this.saving.set(false); },
    });
  }

  protected canEditUserRoles(): boolean { return this.canManageUserRoles; }

  protected permissionDisabled(permission: PermissionDefinition): boolean {
    return permission.isProtectedForAdministrator;
  }

  private loadRoles(): void {
    this.api.listRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        if (roles.length > 0 && this.canViewRoles) this.selectRole(roles[0].id);
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.apiError(error)),
    });
  }

  private loadPermissions(): void {
    this.api.listPermissions().subscribe({
      next: (permissions) => this.permissions.set(permissions),
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.apiError(error)),
    });
  }

  private replaceUser(user: UserSummary): void {
    this.users.update((current) => current.map((item) => item.id === user.id ? user : item));
  }

  private setUserBusy(userId: string, busy: boolean): void {
    this.busyUserIds.update((current) => {
      const next = new Set(current);
      if (busy) next.add(userId); else next.delete(userId);
      return next;
    });
  }

  protected userBusy(userId: string): boolean { return this.busyUserIds().has(userId); }

  private apiError(error: HttpErrorResponse): string {
    if (error.status === 0) return 'No se pudo conectar con la API.';
    if (error.status === 401) return 'La sesión venció. Iniciá sesión nuevamente.';
    if (error.status === 403) return 'Tu cuenta no tiene permiso para esta operación.';
    if (error.status === 409) return error.error?.title ?? 'La operación entra en conflicto con una regla de seguridad.';
    if (error.status === 400) {
      const messages = Object.values(error.error?.errors ?? {}).flat() as string[];
      return messages[0] ?? error.error?.title ?? 'Revisá los datos ingresados.';
    }
    return error.error?.title ?? 'No se pudo completar la operación.';
  }
}
