import {Component, inject, input, signal} from '@angular/core';
import {AuthService} from '../../core/auth/auth.service';
import {Router} from '@angular/router';
import {email, form, FormField, required, submit} from '@angular/forms/signals';
import {problemMessage} from '../../core/api/problem';
import {ThemeSwitch} from '../../shared/ui/theme-switch';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';
import {Revealable} from '../../shared/ui/revealable';

@Component({
  imports: [FormField, ThemeSwitch, FaIconComponent, Revealable],
  selector: 'app-login',
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Куда вернуться после входа - приходит из ?returnUrl= */
  readonly returnUrl = input<string>();

  protected readonly model = signal({ email: '', password: '' });

  protected readonly loginForm = form(this.model, (path) => {
    required(path.email, { message: 'Укажите рабочую почту' });
    email(path.email, { message: 'Это не похоже на адрес почты' });
    required(path.password, { message: 'Введите пароль' });
  });

  protected readonly busy = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.serverError.set(null);

    await submit(this.loginForm, async () => {
      this.busy.set(true);
      try {
        const { email, password } = this.model();
        await this.auth.login(email, password);
        await this.router.navigateByUrl(this.safeReturnUrl());
      } catch (error) {
        this.serverError.set(problemMessage(error, 'Не удалось войти. Попробуйте ещё раз.'));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }

  /** Только адреса внутри приложения - чтобы ссылкой ?returnUrl= нельзя было увести на чужой сайт. */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
  }
}
