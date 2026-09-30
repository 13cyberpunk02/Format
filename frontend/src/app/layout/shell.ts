import { Component, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { PageHeader } from '../core/page-header';
import { RulesDialog } from '../shared/ui/rules-dialog';
import { ThemeSwitch } from '../shared/ui/theme-switch';
import { Sidebar } from './sidebar';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, Sidebar, ThemeSwitch, RulesDialog],
  templateUrl: './shell.html',
})
export class Shell {
  protected readonly header = inject(PageHeader);
  protected readonly rules = viewChild.required(RulesDialog);

  /** Боковое меню на узком экране */
  protected readonly menuOpen = signal(false);

  constructor() {
    // После перехода на другой экран мобильное меню закрываем
    inject(Router)
      .events.pipe(
      filter((event) => event instanceof NavigationEnd),
      takeUntilDestroyed(),
    )
      .subscribe(() => this.menuOpen.set(false));
  }
}
