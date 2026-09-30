import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { problemMessage } from '../../core/api/problem';
import { CurrentUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../core/page-header';
import { ROLE_LABELS } from '../admin/users/user-labels';
import { ChangePasswordForm } from './change-password-form';

@Component({
  selector: 'app-profile',
  imports: [ChangePasswordForm],
  templateUrl: './profile.html',
})
export class Profile {
  private readonly auth = inject(AuthService);

  /** Свежие данные из базы, а не снимок из токена. */
  protected readonly me = httpResource<CurrentUser>(() => '/api/auth/me');

  protected readonly initials = computed(() =>
    (this.me.hasValue() ? this.me.value().displayName : '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((word) => word[0].toUpperCase())
      .join(''),
  );

  protected readonly roleLabels = ROLE_LABELS;

  protected readonly passwordChanged = signal(false);
  protected readonly confirmingLogoutAll = signal(false);
  protected readonly logoutBusy = signal(false);
  protected readonly logoutError = signal<string | null>(null);

  constructor() {
    inject(PageHeader).set('Учётная запись', 'Профиль');
  }

  protected async logoutEverywhere(): Promise<void> {
    this.logoutBusy.set(true);
    this.logoutError.set(null);
    try {
      await this.auth.logoutEverywhere();
    } catch (error) {
      this.logoutError.set(problemMessage(error, 'Не удалось завершить сеансы.'));
      this.logoutBusy.set(false);
    }
  }
}
