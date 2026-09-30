import { Service, computed, effect, signal } from '@angular/core';
import { Drawing } from '../../core/api/models';
import { MAX_COPIES, MIN_COPIES } from '../../shared/ui/copies-stepper';

export interface DraftItem {
  drawing: Drawing;
  copies: number;
}

/** Листы одного загруженного файла - в интерфейсе показываются вместе. */
export interface DraftGroup {
  uploadId: string;
  fileName: string;
  pageCount: number;
  items: DraftItem[];
}

const STORAGE_KEY = 'format-order-draft';

@Service()
export class OrderDraft {
  private readonly items = signal<DraftItem[]>(loadDraft());

  readonly entries = this.items.asReadonly();
  readonly isEmpty = computed(() => this.items().length === 0);
  readonly drawingIds = computed(() => new Set(this.items().map((item) => item.drawing.id)));

  readonly groups = computed(() => {
    const groups = new Map<string, DraftGroup>();
    for (const item of this.items()) {
      const { uploadId, fileName, pageCount } = item.drawing;
      let group = groups.get(uploadId);
      if (!group) {
        group = { uploadId, fileName, pageCount, items: [] };
        groups.set(uploadId, group);
      }
      group.items.push(item);
    }
    return [...groups.values()];
  });

  constructor() {
    effect(() => saveDraft(this.items()));
  }

  /** Добавить чертежи по 1 копии. Уже добавленные пропускаются. */
  add(drawings: readonly Drawing[]): void {
    this.items.update((items) => {
      const known = new Set(items.map((item) => item.drawing.id));
      const added = drawings
        .filter((drawing) => !known.has(drawing.id))
        .map((drawing) => ({ drawing, copies: MIN_COPIES }));
      return added.length > 0 ? [...items, ...added] : items;
    });
  }

  setCopies(drawingId: string, copies: number): void {
    this.items.update((items) =>
      items.map((item) => (item.drawing.id === drawingId ? { ...item, copies: clamp(copies) } : item)),
    );
  }

  setCopiesForUpload(uploadId: string, copies: number): void {
    this.items.update((items) =>
      items.map((item) => (item.drawing.uploadId === uploadId ? { ...item, copies: clamp(copies) } : item)),
    );
  }

  remove(drawingId: string): void {
    this.items.update((items) => items.filter((item) => item.drawing.id !== drawingId));
  }

  removeUpload(uploadId: string): void {
    this.items.update((items) => items.filter((item) => item.drawing.uploadId !== uploadId));
  }

  clear(): void {
    this.items.set([]);
  }
}

function clamp(copies: number): number {
  return Math.min(MAX_COPIES, Math.max(MIN_COPIES, Math.round(copies)));
}

function loadDraft(): DraftItem[] {
  try {
    const saved = sessionStorage.getItem(STORAGE_KEY);
    return saved ? (JSON.parse(saved) as DraftItem[]) : [];
  } catch {
    return [];
  }
}

function saveDraft(items: DraftItem[]): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    // хранилище недоступно или переполнено - черновик просто не переживёт перезагрузку
  }
}
