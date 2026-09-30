import { DOCUMENT, Service, effect, inject, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

const STORAGE_KEY = 'format-theme';

@Service()
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  /** Начальное значение берём из атрибута, который уже выставил скрипт в index.html */
  readonly theme = signal<Theme>(
    this.document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark',
  );

  constructor() {
    effect(() => {
      const theme = this.theme();
      this.document.documentElement.setAttribute('data-theme', theme);
      try {
        localStorage.setItem(STORAGE_KEY, theme);
      } catch {
        // хранилище недоступно - тема просто не запомнится
      }
    });
  }

  set(theme: Theme): void {
    this.theme.set(theme);
  }
}
