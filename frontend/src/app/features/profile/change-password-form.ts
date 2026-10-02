import { Component, computed, inject, output, signal } from '@angular/core';
import { FormField, form, minLength, required, submit } from '@angular/forms/signals';
import { problemMessage } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import {Revealable} from '../../shared/ui/revealable';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

@Component({
  selector: 'app-change-password-form',
  imports: [FormField, Revealable, FaIconComponent],
  template: `
    <form class="flex flex-col gap-3" novalidate (submit)="onSubmit($event)">
      @let current = passwordForm.currentPassword();
      <label class="flex flex-col gap-1.5">
        <span class="flex items-center gap-1.5 text-meta text-ink-5">
          <fa-icon class="icon" icon="key" />
          Текущий пароль
        </span>
        <input
          class="field"
          type="password"
          autocomplete="current-password"
          [formField]="passwordForm.currentPassword"
          [attr.aria-invalid]="current.touched() && current.invalid()"
        />
        @if (current.touched() && current.invalid()) {
          <span class="text-xs text-warn">{{ current.errors()[0]?.message }}</span>
        }
      </label>

      @let next = passwordForm.newPassword();
      <label class="flex flex-col gap-1.5">
        <span class="text-meta text-ink-5">Новый пароль</span>
        <div class="relative">
          <input
            #nextInput="revealable"
            appRevealable
            class="field pr-11"
            autocomplete="new-password"
            [formField]="passwordForm.newPassword"
            [attr.aria-invalid]="next.touched() && next.invalid()"
          />
          <button
            type="button"
            class="absolute inset-y-0 right-0 grid w-11 place-items-center text-ink-6 hover:text-ink"
            [attr.aria-label]="nextInput.visible() ? 'Скрыть пароль' : 'Показать пароль'"
            [attr.aria-pressed]="nextInput.visible()"
            (click)="nextInput.toggle()"
          >
            <fa-icon [icon]="nextInput.visible() ? 'eye-slash' : 'eye'" />
          </button>
        </div>
        @if (next.touched() && next.invalid()) {
          <span class="text-xs text-warn">{{ next.errors()[0]?.message }}</span>
        } @else {
          <span class="text-xs text-ink-6">Не короче 8 символов. Лучше несколько слов, чем сложные символы.</span>
        }
      </label>

      <label class="flex flex-col gap-1.5">
        <span class="text-meta text-ink-5">Новый пароль ещё раз</span>
        <div class="relative">
          <input
            #confirmInput="revealable"
            appRevealable
            class="field pr-11"
            autocomplete="new-password"
            [formField]="passwordForm.confirm"
            [attr.aria-invalid]="mismatch()"
          />
          <button
            type="button"
            class="absolute inset-y-0 right-0 grid w-11 place-items-center text-ink-6 hover:text-ink"
            [attr.aria-label]="confirmInput.visible() ? 'Скрыть пароль' : 'Показать пароль'"
            [attr.aria-pressed]="confirmInput.visible()"
            (click)="confirmInput.toggle()"
          >
            <fa-icon [icon]="confirmInput.visible() ? 'eye-slash' : 'eye'" />
          </button>
        </div>

        @if (mismatch()) {
          <span class="text-xs text-warn">Пароли не совпадают</span>
        }
      </label>

      @if (error(); as message) {
        <span class="text-xs text-warn" role="alert">{{ message }}</span>
      }

      <button type="submit" class="btn-primary" [disabled]="busy()">
        {{ busy() ? 'Сохраняем…' : 'Сменить пароль' }}
      </button>
    </form>
  `,
})
export class ChangePasswordForm {
  readonly changed = output<void>();

  private readonly auth = inject(AuthService);

  protected readonly model = signal({ currentPassword: '', newPassword: '', confirm: '' });

  protected readonly passwordForm = form(this.model, (path) => {
    required(path.currentPassword, { message: 'Введите текущий пароль' });
    required(path.newPassword, { message: 'Введите новый пароль' });
    required(path.confirm, { message: 'Введите новый пароль еще раз' });
    minLength(path.newPassword, 8, { message: 'Не короче 8 символов' });
  });

  /** Повтор показываем как ошибку, только когда в нём уже что-то введено. */
  protected readonly mismatch = computed(() => {
    const { newPassword, confirm } = this.model();
    return confirm.length > 0 && confirm !== newPassword;
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set(null);

    await submit(this.passwordForm, async () => {
      const { currentPassword, newPassword, confirm } = this.model();
      if (confirm !== newPassword) {
        this.error.set('Повторите новый пароль без ошибок.');
        return undefined;
      }

      this.busy.set(true);
      try {
        await this.auth.changePassword(currentPassword, newPassword);
        this.changed.emit();
      } catch (error) {
        this.error.set(problemMessage(error, 'Не удалось сменить пароль.'));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }
}
