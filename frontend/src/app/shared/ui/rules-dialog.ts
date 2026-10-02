import {Component, ElementRef, inject, signal, viewChild} from '@angular/core';
import {FormatsService} from '../../core/formats.service';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

@Component({
  selector: 'app-rules-dialog',
  templateUrl: './rules-dialog.html',
  imports: [
    FaIconComponent
  ]
})
export class RulesDialog {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly formatsService = inject(FormatsService);

  protected readonly formats = this.formatsService.resource;

  open(): void {
    this.formatsService.load();
    this.dialog().nativeElement.showModal();
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  protected onDialogClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.close();
    }
  }
}
