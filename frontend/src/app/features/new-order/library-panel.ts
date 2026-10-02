import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DrawingsApi } from '../../core/api/drawings.api';
import { Drawing, Paged } from '../../core/api/models';
import { problemMessage } from '../../core/api/problem';
import { FormatsService } from '../../core/formats.service';
import { SheetThumb } from '../../shared/ui/sheet-thumb';
import { OrderDraft } from './order-draft';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-library-panel',
  imports: [SheetThumb, FaIconComponent],
  templateUrl: './library-panel.html',
})
export class LibraryPanel {
  private readonly api = inject(DrawingsApi);
  protected readonly draft = inject(OrderDraft);
  protected readonly formats = inject(FormatsService);

  protected readonly page = signal(1);
  protected readonly search = signal('');
  protected readonly format = signal('');
  protected readonly addingUpload = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  private readonly params = computed(() => {
    const params: Record<string, string | number> = { page: this.page(), pageSize: PAGE_SIZE };
    if (this.search()) params['search'] = this.search();
    if (this.format()) params['format'] = this.format();
    return params;
  });

  protected readonly drawings = httpResource<Paged<Drawing>>(() => ({
    url: '/api/drawings',
    params: this.params(),
  }));

  protected readonly totalPages = computed(() =>
    this.drawings.hasValue() ? Math.max(1, Math.ceil(this.drawings.value().total / PAGE_SIZE)) : 1,
  );

  constructor() {
    this.formats.load();
  }

  protected applySearch(value: string): void {
    this.search.set(value.trim());
    this.page.set(1);
  }

  protected applyFormat(value: string): void {
    this.format.set(value);
    this.page.set(1);
  }

  /** Добавить все листы комплекта, из которого этот чертёж. */
  protected async addWholeUpload(drawing: Drawing): Promise<void> {
    this.addingUpload.set(drawing.uploadId);
    this.error.set(null);
    try {
      this.draft.add(await this.api.uploadPages(drawing.uploadId));
    } catch (error) {
      this.error.set(problemMessage(error, 'Не удалось получить листы комплекта.'));
    } finally {
      this.addingUpload.set(null);
    }
  }
}
