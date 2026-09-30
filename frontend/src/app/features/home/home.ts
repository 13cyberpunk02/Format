import {Component, inject, signal} from '@angular/core';
import {AuthService} from '../../core/auth/auth.service';
import {PageHeader} from '../../core/page-header';

@Component({
  imports: [],
  selector: 'app-home',
  templateUrl: './home.html',
})
export class Home {
  constructor() {
    const user = inject(AuthService).user();
    inject(PageHeader).set(`${greeting()}, ${firstName(user?.displayName ?? '')}`, 'Печать чертежей');
  }
}

function greeting(): string {
  const hour = new Date().getHours();
  if (hour < 5) return 'Доброй ночи';
  if (hour < 12) return 'Доброе утро';
  if (hour < 18) return 'Добрый день';
  return 'Добрый вечер';
}

/** «Иванов Иван Иванович» → «Иван». Если слово одно - оно и есть имя. */
function firstName(displayName: string): string {
  const words = displayName.trim().split(/\s+/);
  return words.length >= 2 ? words[1] : words[0];
}
