import { Component, inject, output, signal } from '@angular/core';
import { FormField, email, form, minLength, required, submit } from '@angular/forms/signals';
import { AdminUser } from '../../../core/api/models';
import { problemMessage } from '../../../core/api/problem';
import { UsersApi } from '../../../core/api/users.api';
import { UserRole } from '../../../core/auth/auth.models';
import { generatePassword } from '../../../shared/password';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

export interface CreatedUser {
  user: AdminUser;
  password: string;
}

@Component({
  selector: 'app-user-create-form',
  imports: [FormField, FaIconComponent],
  templateUrl: './user-create-form.html',
})
export class UserCreateForm {
  readonly created = output<CreatedUser>();
  readonly cancelled = output<void>();

  private readonly api = inject(UsersApi);

  protected readonly model = signal({
    email: '',
    displayName: '',
    department: '',
    password: generatePassword(),
    role: 'User' as UserRole,
  });

  protected readonly userForm = form(this.model, (path) => {
    required(path.email, { message: 'Укажите рабочую почту' });
    email(path.email, { message: 'Это не похоже на адрес почты' });
    required(path.displayName, { message: 'Укажите ФИО' });
    required(path.department, { message: 'Укажите отдел' });
    required(path.password, { message: 'Задайте пароль' });
    minLength(path.password, 8, { message: 'Не короче 8 символов' });
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected regeneratePassword(): void {
    this.model.update((model) => ({ ...model, password: generatePassword() }));
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set(null);

    await submit(this.userForm, async () => {
      this.busy.set(true);
      try {
        const request = this.model();
        const user = await this.api.create(request);
        this.created.emit({ user, password: request.password });
      } catch (error) {
        this.error.set(problemMessage(error, 'Не удалось создать пользователя.'));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }
}
