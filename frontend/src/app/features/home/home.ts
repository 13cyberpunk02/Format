import { httpResource } from '@angular/common/http';
import { Component, computed, effect, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Order, Paged, PrintSummary } from '../../core/api/models';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../core/page-header';
import { PrintStatusService } from '../../core/print-status.service';
import { translateCupsMessage } from '../../shared/cups-messages';
import { formatDateTime, formatMeters, orderLabel } from '../../shared/format';
import { PRINTER_STATE_LABELS, isPrinterProblem } from '../../shared/printer-state';
import { StatusChip } from '../../shared/ui/status-chip';
import { isActive } from '../orders/order-status';

const REFRESH_MS = 10_000;

@Component({
  selector: 'app-home',
  imports: [RouterLink, StatusChip],
  templateUrl: './home.html',
})
export class Home {
  protected readonly printStatus = inject(PrintStatusService);

  /** Полночь первого числа по местному времени - в UTC, без плюса в адресе. */
  private readonly monthStart = (() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  })();

  protected readonly summary = httpResource<PrintSummary>(() => ({
    url: '/api/print/summary',
    params: { since: this.monthStart },
  }));

  protected readonly recent = httpResource<Paged<Order>>(() => ({
    url: '/api/print/orders',
    params: { pageSize: 5 },
  }));

  protected readonly stats = computed(() => {
    const summary = this.summary.hasValue() ? this.summary.value() : null;
    return [
      { label: 'В работе', note: 'мои задания', value: summary ? String(summary.active) : '-', accent: true },
      { label: 'Напечатано', note: 'за этот месяц', value: summary ? String(summary.completedSince) : '-', accent: false },
      { label: 'Расход рулона', note: 'за этот месяц', value: summary ? formatMeters(summary.rollMmSince) : '-', accent: false },
    ];
  });

  protected readonly orderLabel = orderLabel;
  protected readonly formatDateTime = formatDateTime;
  protected readonly stateLabels = PRINTER_STATE_LABELS;
  protected readonly isProblem = isPrinterProblem;
  protected readonly translate = translateCupsMessage;

  constructor() {
    const user = inject(AuthService).user();
    inject(PageHeader).set(`${greeting()}, ${firstName(user?.displayName ?? '')}`, 'Печать чертежей');

    // Пока среди последних заказов есть незавершённые - обновляем и список, и сводку
    effect((onCleanup) => {
      const hasActive = this.recent.hasValue() && this.recent.value().items.some((o) => isActive(o.status));
      if (!hasActive) return;

      const timer = setInterval(() => {
        this.recent.reload();
        this.summary.reload();
      }, REFRESH_MS);
      onCleanup(() => clearInterval(timer));
    });
  }
}

function greeting(): string {
  const hour = new Date().getHours();
  if (hour < 5) return 'Доброй ночи';
  if (hour < 12) return 'Доброе утро';
  if (hour < 18) return 'Добрый день';
  return 'Добрый вечер';
}

function firstName(displayName: string): string {
  const words = displayName.trim().split(/\s+/);
  return words.length >= 2 ? words[1] : words[0];
}
