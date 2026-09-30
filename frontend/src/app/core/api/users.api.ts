import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserRole } from '../auth/auth.models';
import { AccountStatus, AdminUser } from './models';

export interface CreateUserRequest {
  email: string;
  displayName: string;
  department: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  displayName?: string;
  department?: string;
  role?: UserRole;
  status?: AccountStatus;
}

@Service()
export class UsersApi {
  private readonly http = inject(HttpClient);

  create(request: CreateUserRequest): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>('/api/auth/users', request));
  }

  update(id: string, request: UpdateUserRequest): Promise<AdminUser> {
    return firstValueFrom(this.http.patch<AdminUser>(`/api/auth/users/${id}`, request));
  }

  setPassword(id: string, newPassword: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/auth/users/${id}/password`, { newPassword }));
  }
}
