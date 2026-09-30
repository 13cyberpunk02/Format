import { Routes } from '@angular/router';
import {adminGuard, authGuard, guestGuard} from './core/auth/auth.guards';

const placeholder = () => import('./features/placeholder').then((m) => m.Placeholder);

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        path: 'new',
        loadComponent: placeholder,
        data: { crumb: 'Черновик задания', title: 'Новое задание на печать', step: 4 },
      },
      {
        path: 'orders',
        loadComponent: placeholder,
        data: { crumb: 'Мои заказы', title: 'Мои задания', step: 5 },
      },
      {
        path: 'orders/:id',
        loadComponent: placeholder,
        data: { crumb: 'Заказ', title: 'Карточка задания', step: 5 },
      },
      {
        path: 'profile',
        loadComponent: placeholder,
        data: { crumb: 'Учётная запись', title: 'Профиль', step: 7 },
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        children: [
          {
            path: 'queue',
            loadComponent: placeholder,
            data: { crumb: 'Администрирование', title: 'Очередь цеха', step: 7 },
          },
          {
            path: 'orders',
            loadComponent: placeholder,
            data: { crumb: 'Администрирование', title: 'Все заказы', step: 7 },
          },
          {
            path: 'users',
            loadComponent: placeholder,
            data: { crumb: 'Администрирование', title: 'Пользователи', step: 7 },
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
