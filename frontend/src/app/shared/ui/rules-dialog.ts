import { httpResource } from '@angular/common/http';
import { Component, ElementRef, signal, viewChild } from '@angular/core';

interface FormatInfo {
  name: string;
  shortSide: number;
  longSide: number;
  printer: 'plotter' | 'office';
  isPrintable: boolean;
  reason: string | null;
}

@Component({
  selector: 'app-rules-dialog',
  templateUrl: './rules-dialog.html',
})
export class RulesDialog {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  /** Список форматов загружается при первом открытии окна, не раньше. */
  private readonly wasOpened = signal(false);

  protected readonly formats = httpResource<FormatInfo[]>(() =>
    this.wasOpened() ? '/api/drawings/formats' : undefined,
  );

  open(): void {
    this.wasOpened.set(true);
    this.dialog().nativeElement.showModal();
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  /** Клик по затемнённому фону (мимо содержимого) закрывает окно. */
  protected onDialogClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.close();
    }
  }
}
