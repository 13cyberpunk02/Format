import { httpResource } from '@angular/common/http';
import { Service, computed, effect, inject } from '@angular/core';
import { PrintStatus } from './api/models';
import { AuthService } from './auth/auth.service';

const REFRESH_MS = 30_000;

/** Состояние принтеров и очереди - одно на всё приложение, обновляется раз в 30 секунд. */
@Service()
export class PrintStatusService {
  private readonly auth = inject(AuthService);

  readonly status = httpResource<PrintStatus>(() =>
    this.auth.isAuthenticated() ? '/api/print/status' : undefined,
  );

  readonly plotter = computed(() =>
    this.status.hasValue() ? (this.status.value().printers.find((p) => p.key === 'plotter') ?? null) : null,
  );

  constructor() {
    effect((onCleanup) => {
      if (!this.auth.isAuthenticated()) return;
      const timer = setInterval(() => this.status.reload(), REFRESH_MS);
      onCleanup(() => clearInterval(timer));
    });
  }
}
