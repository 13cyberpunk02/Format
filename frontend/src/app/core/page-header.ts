import { Service, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';

/** Текст шапки текущего экрана. Каждый экран задаёт его сам. */
@Service()
export class PageHeader {
  private readonly documentTitle = inject(Title);

  readonly crumb = signal('');
  readonly title = signal('');

  set(crumb: string, title: string): void {
    this.crumb.set(crumb);
    this.title.set(title);
    this.documentTitle.setTitle(title ? `${title} · Формат` : 'Формат');
  }
}
