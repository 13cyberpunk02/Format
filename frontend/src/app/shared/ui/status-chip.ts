import { Component, input } from '@angular/core';
import { OrderStatus } from '../../core/api/models';
import { ORDER_STATUS_LABELS } from '../../features/orders/order-status';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

const STYLES: Record<OrderStatus, string> = {
  Queued: 'bg-chip text-ink-4',
  Processing: 'bg-acc-soft text-acc-t',
  Printing: 'bg-acc text-on-acc',
  Completed: 'bg-ok-bg text-acc-t',
  Failed: 'bg-warn-chip text-warn',
  Cancelled: 'bg-line-3 text-ink-6',
};

const ICONS: Record<OrderStatus, string> = {
  Queued: 'clock',
  Processing: 'gear',
  Printing: 'print',
  Completed: 'check',
  Failed: 'triangle-exclamation',
  Cancelled: 'ban',
};

@Component({
  selector: 'app-status-chip',
  template: `
    <span class="inline-flex shrink-0 items-center gap-1.5 rounded-full px-[11px] py-[5px] text-[11.5px]"
          [class]="styles[status()]">
      <fa-icon class="icon" [icon]="icons[status()]" [animation]="status() === 'Processing' ? 'spin' : undefined"/>
      {{ labels[status()] }}
    </span>
  `,
  imports: [
    FaIconComponent
  ]
})
export class StatusChip {
  readonly status = input.required<OrderStatus>();
  protected readonly icons = ICONS;
  protected readonly styles = STYLES;
  protected readonly labels = ORDER_STATUS_LABELS;
}
