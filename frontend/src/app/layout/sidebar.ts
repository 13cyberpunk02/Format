import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { PrintStatusService } from '../core/print-status.service';
import { translateCupsMessage } from '../shared/cups-messages';
import { PRINTER_STATE_LABELS, isPrinterProblem } from '../shared/printer-state';

interface NavItem {
  label: string;
  link: string;
  exact?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.html',
})
export class Sidebar {
  protected readonly auth = inject(AuthService);

  protected readonly printStatus = inject(PrintStatusService);
  protected readonly stateLabels = PRINTER_STATE_LABELS;
  protected readonly isProblem = isPrinterProblem;
  protected readonly translate = translateCupsMessage;

  protected readonly mainItems: NavItem[] = [
    { label: 'Главная', link: '/', exact: true },
    { label: 'Новое задание', link: '/new' },
    { label: 'Мои задания', link: '/orders' },
  ];

  protected readonly adminItems: NavItem[] = [
    { label: 'Очередь цеха', link: '/admin/queue' },
    { label: 'Все заказы', link: '/admin/orders' },
    { label: 'Пользователи', link: '/admin/users' },
  ];

  /** «Иванов Иван Иванович» → «ИИ» */
  protected readonly initials = computed(() =>
    (this.auth.user()?.displayName ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((word) => word[0].toUpperCase())
      .join(''),
  );
}
