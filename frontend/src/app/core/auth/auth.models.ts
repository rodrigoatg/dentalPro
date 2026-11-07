export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  userId: string;
  userName: string;
  roles: string[];
  accessToken: string;
  expiresAt: string;
}

export interface CurrentUser {
  id: string;
  userName: string;
  email: string | null;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: string[];
  effectivePermissions: string[];
}
