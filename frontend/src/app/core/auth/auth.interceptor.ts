import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Адреса, которым токен не нужен: они работают по паролю или по cookie. */
const PUBLIC_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || PUBLIC_ENDPOINTS.includes(request.url)) {
    return next(request);
  }

  const auth = inject(AuthService);

  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      // Токен истёк - продлеваем один раз и повторяем запрос
      return from(auth.refresh()).pipe(
        switchMap((renewed) =>
          renewed
            ? next(withToken(request, auth.accessToken()))
            : from(auth.sessionExpired()).pipe(switchMap(() => throwError(() => error))),
        ),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
