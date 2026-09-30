import {Component, inject, signal} from '@angular/core';
import {AuthService} from '../../core/auth/auth.service';
import {HttpClient} from '@angular/common/http';
import {firstValueFrom} from 'rxjs';
import {problemMessage} from '../../core/api/problem';

@Component({
  imports: [],
  selector: 'app-home',
  templateUrl: './home.html',
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);

  protected readonly apiResult = signal<string | null>(null);

  protected async checkApi(): Promise<void> {
    try {
      const me = await firstValueFrom(this.http.get<{ email: string }>('/api/auth/me'));
      this.apiResult.set(`${new Date().toLocaleTimeString()} · ответ получен: ${me.email}`);
    } catch (error) {
      this.apiResult.set(problemMessage(error));
    }
  }
}
