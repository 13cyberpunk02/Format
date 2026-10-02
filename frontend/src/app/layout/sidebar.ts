import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { PrintStatusService } from '../core/print-status.service';
import { translateCupsMessage } from '../shared/cups-messages';
import {PRINTER_STATE_LABELS, isPrinterProblem, PRINTER_STATE_ICONS, printerKindIcon} from '../shared/printer-state';
import {FaIconComponent} from '@fortawesome/angular-fontawesome';

interface NavItem {
  label: string;
  link: string;
  icon: string;
  exact?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, FaIconComponent],
  templateUrl: './sidebar.html',
})
export class Sidebar {
  protected readonly auth = inject(AuthService);

  protected readonly printStatus = inject(PrintStatusService);
  protected readonly stateLabels = PRINTER_STATE_LABELS;
  protected readonly isProblem = isPrinterProblem;
  protected readonly translate = translateCupsMessage;
  protected readonly stateIcons = PRINTER_STATE_ICONS;
  protected readonly kindIcon = printerKindIcon;

  protected readonly mainItems: NavItem[] = [
    { label: 'Главная', link: '/', icon: 'house', exact: true },
    { label: 'Новое задание', link: '/new', icon: 'plus' },
    { label: 'Мои задания', link: '/orders', icon: 'list-check' },
  ];

  protected readonly adminItems: NavItem[] = [
    { label: 'Очередь цеха', link: '/admin/queue', icon: 'print' },
    { label: 'Все заказы', link: '/admin/orders', icon: 'layer-group' },
    { label: 'Пользователи', link: '/admin/users', icon: 'users' },
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
