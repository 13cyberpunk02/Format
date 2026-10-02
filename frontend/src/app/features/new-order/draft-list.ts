import { Component, inject, signal } from '@angular/core';
import { FormatsService } from '../../core/formats.service';
import { plural } from '../../shared/format';
import { CopiesStepper } from '../../shared/ui/copies-stepper';
import { SheetThumb } from '../../shared/ui/sheet-thumb';
import { DraftGroup, OrderDraft } from './order-draft';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

@Component({
  selector: 'app-draft-list',
  imports: [CopiesStepper, SheetThumb, FaIconComponent],
  templateUrl: './draft-list.html',
})
export class DraftList {
  protected readonly draft = inject(OrderDraft);
  protected readonly formats = inject(FormatsService);
  protected readonly plural = plural;

  /** Свёрнутые комплекты. По умолчанию все развёрнуты. */
  protected readonly collapsed = signal<ReadonlySet<string>>(new Set());

  constructor() {
    this.formats.load();
  }

  protected toggle(uploadId: string): void {
    this.collapsed.update((set) => {
      const next = new Set(set);
      if (next.has(uploadId)) next.delete(uploadId);
      else next.add(uploadId);
      return next;
    });
  }

  /** Общее число копий комплекта или null, если у листов оно разное. */
  protected groupCopies(group: DraftGroup): number | null {
    const first = group.items[0].copies;
    return group.items.every((item) => item.copies === first) ? first : null;
  }

  protected deviceLabel(format: string): string {
    const printer = this.formats.printerOf(format);
    return printer === 'office' ? 'принтер' : printer === 'plotter' ? 'плоттер' : '';
  }
}
