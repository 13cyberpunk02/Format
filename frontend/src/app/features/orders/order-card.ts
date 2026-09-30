import { httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DrawingsApi } from '../../core/api/drawings.api';
import { Order, OrderItem } from '../../core/api/models';
import { PrintApi } from '../../core/api/print.api';
import { problemMessage } from '../../core/api/problem';
import { FormatsService } from '../../core/formats.service';
import { PageHeader } from '../../core/page-header';
import { saveBlob } from '../../shared/files';
import { formatDateTime, formatMeters, orderLabel } from '../../shared/format';
import { SheetThumb } from '../../shared/ui/sheet-thumb';
import {
  JOB_STATUS_LABELS,
  ORDER_STATUS_LABELS,
  StepState,
  buildTimeline,
  isActive,
  translateCupsMessage,
} from './order-status';

const REFRESH_MS = 5_000;

@Component({
  selector: 'app-order-card',
  imports: [RouterLink, SheetThumb],
  templateUrl: './order-card.html',
})
export class OrderCard {
  /** Из адреса /orders/:id */
  readonly id = input.required<string>();

  private readonly printApi = inject(PrintApi);
  private readonly drawingsApi = inject(DrawingsApi);
  protected readonly formats = inject(FormatsService);

  protected readonly order = httpResource<Order>(() => `/api/print/orders/${this.id()}`);

  protected readonly timeline = computed(() => (this.order.hasValue() ? buildTimeline(this.order.value()) : []));
  protected readonly canCancel = computed(
    () => this.order.hasValue() && ['Queued', 'Printing'].includes(this.order.value().status),
  );
  protected readonly canRetry = computed(() => this.order.hasValue() && this.order.value().status === 'Failed');

  protected readonly confirmingCancel = signal(false);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly fileError = signal<string | null>(null);

  protected readonly orderLabel = orderLabel;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatMeters = formatMeters;
  protected readonly statusLabels = ORDER_STATUS_LABELS;
  protected readonly jobLabels = JOB_STATUS_LABELS;
  protected readonly translate = translateCupsMessage;

  protected readonly dotStyles: Record<StepState, string> = {
    done: 'border-acc bg-acc',
    now: 'border-acc bg-acc-t animate-blink',
    next: 'border-line-2 bg-transparent',
    failed: 'border-warn bg-warn',
    cancelled: 'border-line-2 bg-line-2',
  };

  constructor() {
    const header = inject(PageHeader);
    this.formats.load();

    // Заголовок - как только заказ загрузился
    effect(() => {
      if (this.order.hasValue()) {
        const order = this.order.value();
        header.set('Мои задания', `${orderLabel(order.number)} · ${order.title || 'без названия'}`);
      } else {
        header.set('Мои задания', 'Задание');
      }
    });

    // Пока заказ не завершён - обновляем каждые 5 секунд
    effect((onCleanup) => {
      if (!this.order.hasValue() || !isActive(this.order.value().status)) return;
      const timer = setInterval(() => this.order.reload(), REFRESH_MS);
      onCleanup(() => clearInterval(timer));
    });
  }

  protected async cancel(): Promise<void> {
    await this.runAction(() => this.printApi.cancel(this.id()), 'Не удалось отменить задание.');
    this.confirmingCancel.set(false);
  }

  protected async retry(): Promise<void> {
    await this.runAction(() => this.printApi.retry(this.id()), 'Не удалось повторить задание.');
  }

  protected async download(item: OrderItem): Promise<void> {
    this.fileError.set(null);
    try {
      const file = await this.drawingsApi.fetchFile(item.drawingId, true);
      saveBlob(file.blob, file.fileName);
    } catch (error) {
      this.fileError.set(problemMessage(error, 'Не удалось получить файл.'));
    }
  }

  protected async view(item: OrderItem): Promise<void> {
    this.fileError.set(null);
    // Вкладку открываем сразу, в момент клика - иначе браузер заблокирует её как всплывающее окно
    const tab = window.open('', '_blank');
    try {
      const file = await this.drawingsApi.fetchFile(item.drawingId, false);
      if (tab) {
        tab.location.href = URL.createObjectURL(file.blob);
      }
    } catch (error) {
      tab?.close();
      this.fileError.set(problemMessage(error, 'Не удалось открыть файл.'));
    }
  }

  private async runAction(action: () => Promise<unknown>, fallback: string): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await action();
      this.order.reload();
    } catch (error) {
      this.actionError.set(problemMessage(error, fallback));
    } finally {
      this.busy.set(false);
    }
  }
}
