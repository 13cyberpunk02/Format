import { httpResource } from '@angular/common/http';
import { Service, computed, signal } from '@angular/core';
import { FormatInfo } from './api/models';

/** Каталог форматов из сервиса хранения. Загружается один раз, по первому требованию. */
@Service()
export class FormatsService {
  private readonly requested = signal(false);

  readonly resource = httpResource<FormatInfo[]>(() =>
    this.requested() ? '/api/drawings/formats' : undefined,
  );

  readonly all = computed(() => (this.resource.hasValue() ? this.resource.value() : []));

  readonly byName = computed(() => new Map(this.all().map((format) => [format.name, format])));

  load(): void {
    this.requested.set(true);
  }

  printerOf(formatName: string): 'plotter' | 'office' | undefined {
    return this.byName().get(formatName)?.printer;
  }
}
