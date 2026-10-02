import { JobStatus, Order, OrderStatus } from '../../core/api/models';
export { translateCupsMessage } from '../../shared/cups-messages';

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  Queued: 'в очереди',
  Processing: 'отправляется',
  Printing: 'печатается',
  Completed: 'напечатано',
  Failed: 'ошибка',
  Cancelled: 'отменено',
};

export const ACTIVE_STATUSES: OrderStatus[] = ['Queued', 'Processing', 'Printing'];

export function isActive(status: OrderStatus): boolean {
  return ACTIVE_STATUSES.includes(status);
}

export const JOB_STATUS_LABELS: Record<JobStatus, string> = {
  Pending: 'в очереди принтера',
  Completed: 'напечатано',
  Failed: 'ошибка',
  Cancelled: 'отменено',
  Replaced: 'заменено при повторе',
};

export const JOB_STATUS_ICONS: Record<JobStatus, string> = {
  Pending: 'clock',
  Completed: 'check',
  Failed: 'triangle-exclamation',
  Cancelled: 'ban',
  Replaced: 'rotate-right',
};

export type StepState = 'done' | 'now' | 'next' | 'failed' | 'cancelled';

export interface TimelineStep {
  label: string;
  time: string | null;
  state: StepState;
  note?: string | null;
}

/** Ход выполнения заказа - из статуса и отметок времени. */
export function buildTimeline(order: Order): TimelineStep[] {
  const { status } = order;
  const somethingPrinted = order.jobs.some((job) => job.status === 'Completed');

  const steps: TimelineStep[] = [
    { label: 'Задание создано', time: order.createdAt, state: 'done' },
    {
      label:
        status === 'Queued' ? 'Ждёт в очереди' : status === 'Processing' ? 'Собираем листы' : 'Листы собраны и отправлены',
      time: order.startedAt,
      state: status === 'Queued' || status === 'Processing' ? 'now' : order.startedAt ? 'done' : 'next',
    },
    {
      label: 'Печать',
      time: null,
      state: status === 'Printing' ? 'now' : status === 'Completed' || somethingPrinted ? 'done' : 'next',
    },
  ];

  switch (status) {
    case 'Completed':
      steps.push({ label: 'Напечатано', time: order.completedAt, state: 'done' });
      break;
    case 'Failed':
      steps.push({ label: 'Ошибка', time: order.completedAt, state: 'failed', note: order.error });
      break;
    case 'Cancelled':
      steps.push({ label: 'Отменено', time: order.completedAt, state: 'cancelled' });
      break;
    default:
      steps.push({ label: 'Напечатано', time: null, state: 'next' });
  }

  return steps;
}
