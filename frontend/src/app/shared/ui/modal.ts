import { Component, ElementRef, input, signal, viewChild } from '@angular/core';

/**
 * Модальное окно на основе <dialog>.
 * Содержимое оборачивайте в @if (modal.isOpen()) - тогда формы внутри создаются заново при каждом открытии.
 */
@Component({
  selector: 'app-modal',
  template: `
    <dialog
      #dialog
      class="m-auto w-[min(560px,calc(100vw-32px))] rounded-card border border-line bg-surf-2 p-0 text-ink shadow-card backdrop:bg-[rgba(0,0,0,0.55)]"
      [attr.aria-label]="title()"
      (close)="isOpen.set(false)"
    >
      <div class="flex max-h-[85vh] flex-col gap-5 overflow-y-auto p-6 *:shrink-0">
        <div class="flex items-start justify-between gap-4">
          <h2 class="font-serif text-[24px] font-normal">{{ title() }}</h2>
          <button type="button" class="btn-ghost px-3 py-1.5 text-xs" (click)="close()">Закрыть</button>
        </div>
        <ng-content />
      </div>
    </dialog>
  `,
})
export class Modal {
  readonly title = input.required<string>();
  readonly isOpen = signal(false);

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  open(): void {
    this.isOpen.set(true);
    this.dialog().nativeElement.showModal();
  }

  close(): void {
    this.dialog().nativeElement.close();
  }
}
