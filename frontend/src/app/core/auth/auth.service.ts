import { HttpClient } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CurrentUser, LoginResponse } from './auth.models';

interface Session {
  token: string;
  user: CurrentUser;
}

@Service()
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** Текущая сессия. Токен - только в памяти, не в localStorage. */
  private readonly session = signal<Session | null>(null);

  /** Идущее прямо сейчас продление - все, кому оно нужно, ждут его. */
  private refreshInFlight: Promise<boolean> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');

  accessToken(): string | null {
    return this.session()?.token ?? null;
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/login', { email, password }),
    );
    this.start(response);
  }

  /**
   * Продлить сессию по cookie с refresh-токеном.
   * Параллельные вызовы получают один и тот же запрос: refresh-токен одноразовый.
   */
  refresh(): Promise<boolean> {
    this.refreshInFlight ??= this.doRefresh().finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } catch {
      // Выходим в любом случае - сессию на этом устройстве забываем
    }
    this.session.set(null);
    await this.router.navigateByUrl('/login');
  }

  /** Сессия закончилась посреди работы: на вход, запомнив, где был пользователь. */
  async sessionExpired(): Promise<void> {
    this.session.set(null);
    await this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }

  private async doRefresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(this.http.post<LoginResponse>('/api/auth/refresh', null));
      this.start(response);
      return true;
    } catch {
      this.session.set(null);
      return false;
    }
  }

  private start(response: LoginResponse): void {
    this.session.set({ token: response.accessToken, user: response.user });
  }
}
