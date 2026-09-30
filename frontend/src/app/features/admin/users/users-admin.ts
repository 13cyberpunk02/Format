import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { AdminUser } from '../../../core/api/models';
import { problemMessage } from '../../../core/api/problem';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { PageHeader } from '../../../core/page-header';
import { formatDateTime } from '../../../shared/format';
import { Modal } from '../../../shared/ui/modal';
import { CreatedUser, UserCreateForm } from './user-create-form';
import { UserEditForm } from './user-edit-form';
import { ACCOUNT_STATUS_LABELS, ROLE_LABELS } from './user-labels';

interface Notice {
  text: string;
  password?: string;
}

@Component({
  selector: 'app-users-admin',
  imports: [Modal, UserCreateForm, UserEditForm],
  templateUrl: './users-admin.html',
})
export class UsersAdmin {
  private readonly api = inject(UsersApi);
  private readonly auth = inject(AuthService);

  protected readonly users = httpResource<AdminUser[]>(() => '/api/auth/users');

  protected readonly query = signal('');
  protected readonly filtered = computed(() => {
    const all = this.users.hasValue() ? this.users.value() : [];
    const query = this.query().trim().toLowerCase();
    return query
      ? all.filter((u) => `${u.displayName} ${u.email} ${u.department}`.toLowerCase().includes(query))
      : all;
  });

  protected readonly currentUserId = computed(() => this.auth.user()?.id);

  protected readonly createModal = viewChild.required<Modal>('createModal');
  protected readonly editModal = viewChild.required<Modal>('editModal');
  protected readonly editing = signal<AdminUser | null>(null);

  protected readonly notice = signal<Notice | null>(null);
  protected readonly rowError = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly statusLabels = ACCOUNT_STATUS_LABELS;
  protected readonly formatDateTime = formatDateTime;

  constructor() {
    inject(PageHeader).set('Администрирование', 'Пользователи');
  }

  protected openCreate(): void {
    this.notice.set(null);
    this.createModal().open();
  }

  protected openEdit(user: AdminUser): void {
    this.notice.set(null);
    this.editing.set(user);
    this.editModal().open();
  }

  protected onCreated({ user, password }: CreatedUser): void {
    this.createModal().close();
    this.users.reload();
    this.notice.set({ text: `${user.displayName} (${user.email}) добавлен. Передайте ему пароль:`, password });
  }

  protected onSaved(user: AdminUser, passwordChanged: string | null): void {
    this.editModal().close();
    this.users.reload();
    this.notice.set(
      passwordChanged
        ? { text: `Изменения для ${user.displayName} сохранены. Новый пароль:`, password: passwordChanged }
        : { text: `Изменения для ${user.displayName} сохранены.` },
    );
  }

  protected async toggleBlocked(user: AdminUser): Promise<void> {
    this.busyId.set(user.id);
    this.rowError.set(null);
    try {
      await this.api.update(user.id, { status: user.status === 'Disabled' ? 'Active' : 'Disabled' });
      this.users.reload();
    } catch (error) {
      this.rowError.set(problemMessage(error, 'Не удалось изменить статус.'));
    } finally {
      this.busyId.set(null);
    }
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      // буфер обмена недоступен (например, не HTTPS) - пароль и так виден на экране
    }
  }
}
