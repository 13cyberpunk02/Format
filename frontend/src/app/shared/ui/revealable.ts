import { Directive, signal } from '@angular/core';

/** Поле пароля, которое можно показать: <input appRevealable #pwd="revealable" />, затем pwd.toggle(). */
@Directive({
  selector: 'input[appRevealable]',
  exportAs: 'revealable',
  host: {
    '[type]': "visible() ? 'text' : 'password'",
  },
})
export class Revealable {
  readonly visible = signal(false);

  toggle(): void {
    this.visible.update((visible) => !visible);
  }
}
