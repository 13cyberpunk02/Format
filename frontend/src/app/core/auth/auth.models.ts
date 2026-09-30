export type UserRole = 'User' | 'Admin';

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  department: string;
  role: UserRole;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}
