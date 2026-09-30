import { Component, input } from '@angular/core';
import { OrderStatus } from '../../core/api/models';
import { ORDER_STATUS_LABELS } from '../../features/orders/order-status';

const STYLES: Record<OrderStatus, string> = {
  Queued: 'bg-chip text-ink-4',
  Processing: 'bg-acc-soft text-acc-t',
  Printing: 'bg-acc text-on-acc',
  Completed: 'bg-ok-bg text-acc-t',
  Failed: 'bg-warn-chip text-warn',
  Cancelled: 'bg-line-3 text-ink-6',
};

@Component({
  selector: 'app-status-chip',
  template: `
    <span class="inline-flex shrink-0 items-center rounded-full px-[11px] py-[5px] text-[11.5px]" [class]="styles[status()]">
      {{ labels[status()] }}
    </span>
  `,
})
export class StatusChip {
  readonly status = input.required<OrderStatus>();

  protected readonly styles = STYLES;
  protected readonly labels = ORDER_STATUS_LABELS;
}
