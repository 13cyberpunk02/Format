import { httpResource } from '@angular/common/http';
import {Component, computed, effect, inject, input, signal} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Order, OrderStatus, Paged } from '../../core/api/models';
import { PageHeader } from '../../core/page-header';
import { formatDateTime, formatMeters, orderLabel } from '../../shared/format';
import { StatusChip } from '../../shared/ui/status-chip';
import { ACTIVE_STATUSES, isActive } from './order-status';

type Filter = 'all' | 'active' | 'completed' | 'failed' | 'cancelled';

const FILTERS: { value: Filter; label: string; statuses: OrderStatus[] }[] = [
  { value: 'all', label: 'Все', statuses: [] },
  { value: 'active', label: 'В работе', statuses: ACTIVE_STATUSES },
  { value: 'completed', label: 'Напечатано', statuses: ['Completed'] },
  { value: 'failed', label: 'С ошибкой', statuses: ['Failed'] },
  { value: 'cancelled', label: 'Отменённые', statuses: ['Cancelled'] },
];

/** Сетка таблицы для двух режимов. Строки целиком — иначе Tailwind не увидит классы. */
const GRID_MINE = 'grid grid-cols-[104px_minmax(200px,1.6fr)_90px_140px_100px_130px]';
const GRID_ALL = 'grid grid-cols-[104px_minmax(200px,1.6fr)_minmax(160px,1fr)_80px_130px_100px_130px]';

const PAGE_SIZE = 20;
const REFRESH_MS = 10_000;

@Component({
  selector: 'app-orders-list',
  imports: [RouterLink, StatusChip],
  templateUrl: './orders-list.html',
})
export class OrdersList {
  /** 'mine' — мои задания, 'all' — все заказы (для администратора). Приходит из data маршрута. */
  readonly scope = input<'mine' | 'all'>('mine');

  protected readonly isAll = computed(() => this.scope() === 'all');
  protected readonly grid = computed(() => (this.isAll() ? GRID_ALL : GRID_MINE));

  protected readonly filters = FILTERS;
  protected readonly filter = signal<Filter>('all');
  protected readonly page = signal(1);
  protected readonly search = signal('');

  protected readonly orderLabel = orderLabel;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatMeters = formatMeters;

  protected readonly orders = httpResource<Paged<Order>>(() => {
    const statuses = FILTERS.find((f) => f.value === this.filter())!.statuses;
    return {
      url: this.isAll() ? '/api/print/admin/orders' : '/api/print/orders',
      params: {
        page: this.page(),
        pageSize: PAGE_SIZE,
        ...(statuses.length > 0 ? { status: statuses } : {}),
        ...(this.isAll() && this.search() ? { search: this.search() } : {}),
      },
    };
  });

  protected readonly totalPages = computed(() =>
    this.orders.hasValue() ? Math.max(1, Math.ceil(this.orders.value().total / PAGE_SIZE)) : 1,
  );

  constructor() {
    const header = inject(PageHeader);
    effect(() =>
      this.isAll()
        ? header.set('Администрирование', 'Все задания')
        : header.set('Печать', 'Мои задания'),
    );

    effect((onCleanup) => {
      const hasActive = this.orders.hasValue() && this.orders.value().items.some((o) => isActive(o.status));
      if (!hasActive) return;

      const timer = setInterval(() => this.orders.reload(), REFRESH_MS);
      onCleanup(() => clearInterval(timer));
    });
  }

  protected setFilter(filter: Filter): void {
    this.filter.set(filter);
    this.page.set(1);
  }

  protected applySearch(value: string): void {
    this.search.set(value.trim());
    this.page.set(1);
  }

  protected formatsOf(order: Order): string {
    const formats = [...new Set(order.items.map((item) => item.format))];
    return formats.length > 3 ? `${formats.slice(0, 3).join(', ')}…` : formats.join(', ');
  }
}
