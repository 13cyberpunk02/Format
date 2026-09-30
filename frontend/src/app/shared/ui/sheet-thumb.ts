import { Component, computed, input } from '@angular/core';

const BOX_WIDTH = 40;
const BOX_HEIGHT = 56;

/** Листок в реальных пропорциях формата, вписанный в квадратик 40×56. */
@Component({
  selector: 'app-sheet-thumb',
  template: `
    <span class="grid shrink-0 place-items-center" [style.width.px]="boxWidth" [style.height.px]="boxHeight">
      <span class="paper-grid rounded-[4px]" [style.width.px]="size().width" [style.height.px]="size().height"></span>
    </span>
  `,
})
export class SheetThumb {
  readonly widthMm = input.required<number>();
  readonly heightMm = input.required<number>();

  protected readonly boxWidth = BOX_WIDTH;
  protected readonly boxHeight = BOX_HEIGHT;

  /** Всегда книжная ориентация: короткая сторона - ширина. */
  protected readonly size = computed(() => {
    const ratio = Math.max(this.widthMm(), this.heightMm()) / Math.min(this.widthMm(), this.heightMm());
    const width = Math.min(BOX_WIDTH, BOX_HEIGHT / ratio);
    return { width: Math.round(width), height: Math.round(width * ratio) };
  });
}
