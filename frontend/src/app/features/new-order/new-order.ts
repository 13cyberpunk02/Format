import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PrintPreview } from '../../core/api/models';
import { PrintApi } from '../../core/api/print.api';
import { problemMessage } from '../../core/api/problem';
import { FormatsService } from '../../core/formats.service';
import { PageHeader } from '../../core/page-header';
import { formatMeters, plural } from '../../shared/format';
import { DraftList } from './draft-list';
import { LibraryPanel } from './library-panel';
import { OrderDraft } from './order-draft';
import { RollPreview } from './roll-preview';
import { SheetCard } from './sheet-card';
import { UploadPanel } from './upload-panel';

type Source = 'upload' | 'library';

@Component({
  selector: 'app-new-order',
  imports: [UploadPanel, LibraryPanel, DraftList, RollPreview, SheetCard],
  templateUrl: './new-order.html',
})
export class NewOrder {
  protected readonly draft = inject(OrderDraft);
  protected readonly formats = inject(FormatsService);
  private readonly printApi = inject(PrintApi);
  private readonly router = inject(Router);

  protected readonly plural = plural;
  protected readonly formatMeters = formatMeters;

  protected readonly source = signal<Source>('upload');
  protected readonly sources: { value: Source; label: string }[] = [
    { value: 'upload', label: 'Загрузить' },
    { value: 'library', label: 'Из загруженных' },
  ];

  private readonly requestItems = computed(() =>
    this.draft.entries().map((entry) => ({ drawingId: entry.drawing.id, copies: entry.copies })),
  );

  /** Раскладка от сервиса печати: пересчитывается при любом изменении черновика. */
  protected readonly preview = httpResource<PrintPreview>(() => {
    const items = this.requestItems();
    return items.length > 0 ? { url: '/api/print/preview', method: 'POST', body: { items } } : undefined;
  });

  /** Последняя удачная раскладка — чтобы схема не пропадала, пока считается новая. */
  protected readonly shownPreview = linkedSignal<
    { value: PrintPreview | undefined; empty: boolean },
    PrintPreview | undefined
  >({
    source: () => ({
      value: this.preview.hasValue() ? this.preview.value() : undefined,
      empty: this.draft.isEmpty(),
    }),
    computation: (source, previous) => (source.empty ? undefined : (source.value ?? previous?.value)),
  });

  protected readonly previewError = computed(() => {
    const error = this.preview.error();
    return error ? problemMessage(error, 'Не удалось рассчитать раскладку.') : null;
  });

  /** Чертежи, которые удалили из хранилища, пока они лежали в черновике. */
  protected readonly missingIds = computed<string[]>(() => {
    const error = this.preview.error();
    return error instanceof HttpErrorResponse && Array.isArray(error.error?.missingDrawingIds)
      ? error.error.missingDrawingIds
      : [];
  });

  protected readonly selectedId = signal<string | null>(null);

  /** Выбранный чертёж или первый в черновике. */
  protected readonly selected = computed(() => {
    const entries = this.draft.entries();
    return entries.find((entry) => entry.drawing.id === this.selectedId()) ?? entries[0] ?? null;
  });

  protected readonly totals = computed(() => {
    const preview = this.shownPreview();
    if (!preview) return null;
    return {
      sheets: preview.sheets.length,
      rollMm: preview.totalRollLengthMm,
      officeCopies: preview.officeJobs.reduce((sum, job) => sum + job.copies, 0),
    };
  });

  protected readonly title = signal('');

  /** То же название, что подставит сервер, если поле оставить пустым. */
  protected readonly defaultTitle = computed(() => {
    const groups = this.draft.groups();
    if (groups.length === 0) return 'Название задания';
    const first = groups[0].fileName.replace(/\.pdf$/i, '');
    return groups.length === 1 ? first : `${first} и ещё ${groups.length - 1}`;
  });

  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);

  protected readonly canSubmit = computed(
    () => this.preview.hasValue() && !this.preview.isLoading() && !this.submitting(),
  );

  constructor() {
    inject(PageHeader).set('Черновик задания', 'Новое задание на печать');
    this.formats.load();
  }

  protected removeMissing(): void {
    for (const id of this.missingIds()) {
      this.draft.remove(id);
    }
  }

  protected async submit(): Promise<void> {
    this.submitting.set(true);
    this.submitError.set(null);

    try {
      const order = await this.printApi.createOrder(this.requestItems(), this.title().trim() || null);
      this.draft.clear();
      this.title.set('');
      await this.router.navigate(['/orders', order.id]);
    } catch (error) {
      this.submitError.set(problemMessage(error, 'Не удалось отправить задание.'));
    } finally {
      this.submitting.set(false);
    }
  }
}
