import { httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminQueue, Order, PrintJob, PrinterStatus } from '../../../core/api/models';
import { PrintApi } from '../../../core/api/print.api';
import { problemMessage } from '../../../core/api/problem';
import { PageHeader } from '../../../core/page-header';
import { PrintStatusService } from '../../../core/print-status.service';
import { translateCupsMessage } from '../../../shared/cups-messages';
import { formatDateTime, orderLabel } from '../../../shared/format';
import { PRINTER_STATE_LABELS, isPrinterProblem } from '../../../shared/printer-state';

const REFRESH_MS = 5_000;

/** Заказ и его незавершённые задания на конкретном принтере. */
interface PrinterEntry {
  order: Order;
  jobs: PrintJob[];
}

@Component({
  selector: 'app-queue',
  imports: [RouterLink],
  templateUrl: './queue.html',
})
export class Queue {
  private readonly printApi = inject(PrintApi);
  private readonly printStatus = inject(PrintStatusService);

  protected readonly queue = httpResource<AdminQueue>(() => '/api/print/admin/queue');

  private readonly active = computed(() => (this.queue.hasValue() ? this.queue.value().active : []));
  protected readonly failed = computed(() => (this.queue.hasValue() ? this.queue.value().failed : []));

  /** Ещё не отправлены на принтеры: в очереди или собираются. */
  protected readonly waiting = computed(() => this.active().filter((o) => o.status !== 'Printing'));

  protected readonly plotter = computed(() => pendingOn(this.active(), 'plotter'));
  protected readonly office = computed(() => pendingOn(this.active(), 'office'));

  protected readonly printers = computed(() => {
    const status = this.printStatus.status;
    const list = status.hasValue() ? status.value().printers : [];
    return {
      plotter: list.find((p) => p.key === 'plotter') ?? null,
      office: list.find((p) => p.key === 'office') ?? null,
    };
  });

  protected readonly retrying = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly orderLabel = orderLabel;
  protected readonly formatDateTime = formatDateTime;
  protected readonly stateLabels = PRINTER_STATE_LABELS;
  protected readonly isProblem = isPrinterProblem;
  protected readonly translate = translateCupsMessage;

  constructor() {
    inject(PageHeader).set('Администрирование', 'Очередь цеха');

    // Экран оператора: обновляем постоянно, пока он открыт
    effect((onCleanup) => {
      const timer = setInterval(() => {
        this.queue.reload();
        this.printStatus.status.reload();
      }, REFRESH_MS);
      onCleanup(() => clearInterval(timer));
    });
  }

  protected printerTone(printer: PrinterStatus | null): string {
    if (!printer) return 'text-ink-6';
    if (this.isProblem(printer.state)) return 'text-warn';
    return printer.state === 'Printing' ? 'text-acc-t' : 'text-ink-6';
  }

  protected async retry(order: Order): Promise<void> {
    this.retrying.set(order.id);
    this.error.set(null);
    try {
      await this.printApi.retry(order.id);
      this.queue.reload();
    } catch (error) {
      this.error.set(problemMessage(error, `Не удалось повторить ${orderLabel(order.number)}.`));
    } finally {
      this.retrying.set(null);
    }
  }
}

function pendingOn(orders: Order[], printer: 'plotter' | 'office'): PrinterEntry[] {
  return orders
    .filter((order) => order.status === 'Printing')
    .map((order) => ({
      order,
      jobs: order.jobs.filter((job) => job.printer === printer && job.status === 'Pending'),
    }))
    .filter((entry) => entry.jobs.length > 0);
}
