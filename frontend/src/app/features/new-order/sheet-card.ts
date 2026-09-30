import { Component, computed, input } from '@angular/core';
import { DraftItem } from './order-draft';

const BOX_WIDTH = 360;
const BOX_HEIGHT = 260;

@Component({
  selector: 'app-sheet-card',
  template: `
    @let drawing = item().drawing;
    <div class="overflow-hidden rounded-card border border-line bg-surf-2">
      <div class="flex items-center justify-between gap-3 border-b border-line-3 px-4 py-3.5">
        <span class="truncate text-[13px] text-ink-2">{{ drawing.fileName }}</span>
        <span class="shrink-0 text-xs text-acc-t">распознано</span>
      </div>

      <div class="flex justify-center bg-well p-5">
        <div
          class="paper-grid relative rounded-md shadow-paper"
          [style.width.px]="size().width"
          [style.height.px]="size().height"
        >
          <div class="absolute inset-2 border border-paper-line"></div>
          <div
            class="absolute right-2 bottom-2 flex w-[44%] flex-col justify-between border-t border-l border-paper-line bg-paper px-2 py-1.5 font-mono text-[9.5px] leading-snug text-paper-ink"
          >
            <span class="truncate">{{ drawing.fileName }}</span>
            <span>лист {{ drawing.pageNumber }} из {{ drawing.pageCount }}</span>
          </div>
          <div class="absolute top-2 left-2 rounded-full bg-acc px-2.5 py-1 font-mono text-[10.5px] text-on-acc">
            {{ drawing.widthMm }} × {{ drawing.heightMm }} мм
          </div>
        </div>
      </div>

      <div class="flex flex-col gap-2 border-t border-line-3 px-4 py-3.5 text-[13px] text-ink-5">
        <div class="flex justify-between gap-3"><span>Формат листа</span><span class="text-ink">{{ drawing.format }}</span></div>
        <div class="flex justify-between gap-3">
          <span>Печатает</span>
          <span class="text-ink">{{ printer() === 'office' ? 'офисный принтер' : 'плоттер Canon TM-300' }}</span>
        </div>
        <div class="flex justify-between gap-3"><span>Копии</span><span class="text-ink">{{ item().copies }}</span></div>
      </div>
    </div>
  `,
})
export class SheetCard {
  readonly item = input.required<DraftItem>();
  readonly printer = input<'plotter' | 'office' | undefined>();

  /** Лист в его настоящих пропорциях и ориентации, вписанный в 360×260. */
  protected readonly size = computed(() => {
    const { widthMm, heightMm } = this.item().drawing;
    const scale = Math.min(BOX_WIDTH / widthMm, BOX_HEIGHT / heightMm);
    return { width: Math.round(widthMm * scale), height: Math.round(heightMm * scale) };
  });
}
