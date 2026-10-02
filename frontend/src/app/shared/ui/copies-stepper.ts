import { Component, input, output } from '@angular/core';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

export const MIN_COPIES = 1;
export const MAX_COPIES = 100;

@Component({
  selector: 'app-copies-stepper',
  template: `
    <div class="flex items-center gap-1.5 rounded-full border border-line-2 p-1">
      <button
        type="button"
        class="grid size-[30px] place-items-center rounded-full bg-chip text-[15px] text-ink-2 disabled:opacity-40"
        aria-label="Меньше копий"
        [disabled]="(value() ?? min) <= min"
        (click)="step(-1)"
      >
        <fa-icon class="text-xs" icon="minus"/>
      </button>
      <span class="min-w-[26px] text-center font-mono text-sm" aria-live="polite">{{ value() ?? '-' }}</span>
      <button
        type="button"
        class="grid size-[30px] place-items-center rounded-full bg-chip text-[15px] text-ink-2 disabled:opacity-40"
        aria-label="Больше копий"
        [disabled]="(value() ?? 0) >= max"
        (click)="step(1)"
      >
        <fa-icon class="text-xs" icon="plus" />
      </button>
    </div>
  `,
  imports: [
    FaIconComponent
  ]
})
export class CopiesStepper {
  /** null - у группы листов разное количество копий. */
  readonly value = input.required<number | null>();
  readonly valueChange = output<number>();

  protected readonly min = MIN_COPIES;
  protected readonly max = MAX_COPIES;

  protected step(delta: number): void {
    const next = (this.value() ?? MIN_COPIES) + delta;
    this.valueChange.emit(Math.min(MAX_COPIES, Math.max(MIN_COPIES, next)));
  }
}
