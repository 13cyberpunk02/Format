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
        loadComponent: () => import('./features/new-order/new-order').then((m) => m.NewOrder),
      },
      {
        path: 'orders',
        loadComponent: () => import('./features/orders/orders-list').then((m) => m.OrdersList),
      },
      {
        path: 'orders/:id',
        loadComponent: () => import('./features/orders/order-card').then((m) => m.OrderCard),
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        children: [
          {
            path: 'queue',
            loadComponent: () => import('./features/admin/queue/queue').then((m) => m.Queue),
          },
          {
            path: 'orders',
            loadComponent: () => import('./features/orders/orders-list').then((m) => m.OrdersList),
            data: { scope: 'all' },
          },
          {
            path: 'users',
            loadComponent: () => import('./features/admin/users/users-admin').then((m) => m.UsersAdmin),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
