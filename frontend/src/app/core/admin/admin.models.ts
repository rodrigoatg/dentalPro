export interface UserSummary {
  id: string;
  userName: string;
  email: string | null;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

export interface RoleSummary {
  id: string;
  name: string;
}

export interface PermissionDefinition {
  code: string;
  name: string;
  description: string | null;
  module: string;
  isProtectedForAdministrator: boolean;
}

export interface RolePermissionSummary {
  roleId: string;
  roleName: string;
  catalog: PermissionDefinition[];
  assignedCodes: string[];
}

export interface CreateUserRequest {
  userName: string;
  email: string | null;
  firstName: string;
  lastName: string;
  password: string;
  roles: string[];
}
