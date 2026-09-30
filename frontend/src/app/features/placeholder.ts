import { Component, effect, inject, input } from '@angular/core';
import { PageHeader } from '../core/page-header';

/** Временный экран: показывает, на каком шаге он появится. */
@Component({
  selector: 'app-placeholder',
  template: `
    <section class="px-[30px] pb-11">
      <div class="flex flex-col items-center gap-2 rounded-card border-[1.5px] border-dashed border-line-dash bg-surf-2 px-6 py-14 text-center">
        <span class="font-serif text-heading">Экран в разработке</span>
        <span class="text-[13px] text-ink-6">Появится на шаге {{ step() }} этапа 8.</span>
      </div>
    </section>
  `,
})
export class Placeholder {
  readonly crumb = input('');
  readonly title = input('');
  readonly step = input<number>();

  constructor() {
    const header = inject(PageHeader);
    effect(() => header.set(this.crumb(), this.title()));
  }
}
