import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Order, PrintItemRequest } from './models';

@Service()
export class PrintApi {
  private readonly http = inject(HttpClient);

  createOrder(items: PrintItemRequest[], title: string | null): Promise<Order> {
    return firstValueFrom(this.http.post<Order>('/api/print/orders', { items, title }));
  }

  cancel(orderId: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/print/orders/${orderId}/cancel`, null));
  }

  retry(orderId: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/print/orders/${orderId}/retry`, null));
  }
}
