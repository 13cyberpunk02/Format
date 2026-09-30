import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DrawingsApi } from '../../core/api/drawings.api';
import { RejectedPage } from '../../core/api/models';
import { problemMessage } from '../../core/api/problem';
import { formatBytes, plural } from '../../shared/format';
import { OrderDraft } from './order-draft';

const MAX_FILE_BYTES = 200 * 1024 * 1024;

interface UploadTask {
  key: number;
  name: string;
  size: number;
  state: 'uploading' | 'done' | 'failed';
  accepted: number;
  rejected: RejectedPage[];
  error?: string;
}

@Component({
  selector: 'app-upload-panel',
  templateUrl: './upload-panel.html',
})
export class UploadPanel {
  private readonly api = inject(DrawingsApi);
  private readonly draft = inject(OrderDraft);

  protected readonly tasks = signal<UploadTask[]>([]);
  protected readonly hasFinished = computed(() => this.tasks().some((task) => task.state !== 'uploading'));

  private dragDepth = 0;
  protected readonly dragging = signal(false);

  private nextKey = 0;

  protected readonly formatBytes = formatBytes;
  protected readonly plural = plural;

  protected onDragEnter(event: DragEvent): void {
    event.preventDefault();
    this.dragDepth++;
    this.dragging.set(true);
  }

  protected onDragOver(event: DragEvent): void {
    // Без preventDefault браузер не разрешит бросить файл сюда и просто откроет его
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  protected onDragLeave(): void {
    this.dragDepth = Math.max(0, this.dragDepth - 1);
    this.dragging.set(this.dragDepth > 0);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragDepth = 0;
    this.dragging.set(false);
    this.addFiles(event.dataTransfer?.files);
  }

  protected onPick(input: HTMLInputElement): void {
    this.addFiles(input.files);
    // Сбрасываем, чтобы повторный выбор того же файла тоже сработал
    input.value = '';
  }

  protected clearFinished(): void {
    this.tasks.update((tasks) => tasks.filter((task) => task.state === 'uploading'));
  }

  private addFiles(files: FileList | null | undefined): void {
    for (const file of Array.from(files ?? [])) {
      const task: UploadTask = {
        key: this.nextKey++,
        name: file.name,
        size: file.size,
        state: 'uploading',
        accepted: 0,
        rejected: [],
      };

      const error = validate(file);
      if (error) {
        this.tasks.update((tasks) => [{ ...task, state: 'failed', error }, ...tasks]);
        continue;
      }

      this.tasks.update((tasks) => [task, ...tasks]);
      void this.upload(task.key, file);
    }
  }

  private async upload(key: number, file: File): Promise<void> {
    try {
      const result = await this.api.upload(file);
      this.draft.add(result.drawings);
      this.patch(key, { state: 'done', accepted: result.drawings.length, rejected: result.rejectedPages });
    } catch (error) {
      this.patch(key, {
        state: 'failed',
        error: problemMessage(error, 'Не удалось загрузить файл.'),
        rejected: rejectedFrom(error),
      });
    }
  }

  private patch(key: number, changes: Partial<UploadTask>): void {
    this.tasks.update((tasks) => tasks.map((task) => (task.key === key ? { ...task, ...changes } : task)));
  }
}

function validate(file: File): string | null {
  const isPdf = file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf');
  if (!isPdf) return 'Принимаются только PDF.';
  if (file.size > MAX_FILE_BYTES) return 'Файл больше 200 МБ.';
  return null;
}

/** Если ни одна страница не подошла, сервис хранения присылает их список вместе с ошибкой 422. */
function rejectedFrom(error: unknown): RejectedPage[] {
  return error instanceof HttpErrorResponse && Array.isArray(error.error?.rejectedPages)
    ? (error.error.rejectedPages as RejectedPage[])
    : [];
}
