import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormField, disabled, form, required, submit } from '@angular/forms/signals';
import { AccountStatus, AdminUser } from '../../../core/api/models';
import { problemMessage } from '../../../core/api/problem';
import { UsersApi } from '../../../core/api/users.api';
import { UserRole } from '../../../core/auth/auth.models';
import { generatePassword } from '../../../shared/password';

@Component({
  selector: 'app-user-edit-form',
  imports: [FormField],
  templateUrl: './user-edit-form.html',
})
export class UserEditForm {
  readonly user = input.required<AdminUser>();
  /** Администратор редактирует самого себя: роль и статус менять нельзя. */
  readonly isSelf = input(false);

  readonly saved = output<{ passwordChanged: string | null }>();
  readonly cancelled = output<void>();

  private readonly api = inject(UsersApi);

  /** Модель формы - копия данных пользователя, которую можно менять. */
  protected readonly model = linkedSignal(() => ({
    displayName: this.user().displayName,
    department: this.user().department,
    role: this.user().role as UserRole,
    status: this.user().status as AccountStatus,
    newPassword: '',
  }));

  protected readonly userForm = form(this.model, (path) => {
    required(path.displayName, { message: 'Укажите ФИО' });
    required(path.department, { message: 'Укажите отдел' });

    // Свою роль и статус администратор менять не может
    disabled(path.role, { when: () => this.isSelf() });
    disabled(path.status, { when: () => this.isSelf() });
  });

  protected readonly passwordError = computed(() => {
    const password = this.model().newPassword;
    return password.length > 0 && password.length < 8 ? 'Не короче 8 символов' : null;
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected generate(): void {
    this.model.update((model) => ({ ...model, newPassword: generatePassword() }));
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set(null);

    await submit(this.userForm, async () => {
      if (this.passwordError()) return undefined;

      this.busy.set(true);
      try {
        const { displayName, department, role, status, newPassword } = this.model();

        await this.api.update(
          this.user().id,
          this.isSelf() ? { displayName, department } : { displayName, department, role, status },
        );

        if (newPassword) {
          await this.api.setPassword(this.user().id, newPassword);
        }

        this.saved.emit({ passwordChanged: newPassword || null });
      } catch (error) {
        this.error.set(problemMessage(error, 'Не удалось сохранить изменения.'));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }
}
