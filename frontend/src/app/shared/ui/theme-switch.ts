import { Component, inject } from '@angular/core';
import { Theme, ThemeService } from '../../core/theme.service';

@Component({
  selector: 'app-theme-switch',
  template: `
    <div class="flex gap-0.5 rounded-full border border-line bg-surf-1 p-[3px]" role="group" aria-label="Тема оформления">
      @for (option of options; track option.value) {
        <button
          type="button"
          class="rounded-full px-3 py-1.5 text-xs font-medium transition-colors"
          [class]="themeService.theme() === option.value ? 'bg-acc text-on-acc' : 'text-ink-5 hover:text-ink'"
          [attr.aria-pressed]="themeService.theme() === option.value"
          (click)="themeService.set(option.value)"
        >
          {{ option.label }}
        </button>
      }
    </div>
  `,
})
export class ThemeSwitch {
  protected readonly themeService = inject(ThemeService);

  protected readonly options: { value: Theme; label: string }[] = [
    { value: 'dark', label: 'Тёмная' },
    { value: 'light', label: 'Светлая' },
  ];
}
