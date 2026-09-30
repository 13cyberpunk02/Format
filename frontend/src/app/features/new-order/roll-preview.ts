import { Component, computed, input, output } from '@angular/core';
import { PreviewSheet } from '../../core/api/models';

/** Промежуток между листами на схеме - место линии реза, мм в координатах схемы. */
const GAP = 60;
const ROLL_WIDTH = 914;

const KIND_LABELS: Record<PreviewSheet['kind'], string> = {
  Single: 'как есть',
  Rotated: 'повёрнут',
  DuplicatePair: 'пара копий',
  Nested: 'два чертежа',
};

@Component({
  selector: 'app-roll-preview',
  templateUrl: './roll-preview.html',
})
export class RollPreview {
  readonly sheets = input.required<PreviewSheet[]>();
  readonly selectedId = input<string | null>(null);
  readonly select = output<string>();

  protected readonly rollWidth = ROLL_WIDTH;
  protected readonly kindLabels = KIND_LABELS;

  /** Каждому листу - отступ сверху; общая высота схемы. */
  protected readonly layout = computed(() => {
    let top = GAP;
    const items = this.sheets().map((sheet) => {
      const item = { sheet, top };
      top += sheet.lengthMm + GAP;
      return item;
    });
    return { items, height: top };
  });
}
