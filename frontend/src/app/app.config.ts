import {ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners} from '@angular/core';
import {provideRouter, withComponentInputBinding} from '@angular/router';
import { routes } from './app.routes';
import {provideHttpClient, withInterceptors} from '@angular/common/http';
import {authInterceptor} from './core/auth/auth.interceptor';
import {AuthService} from './core/auth/auth.service';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { APP_ICONS } from './core/icons';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),

    // Иконки — один раз при запуске, дальше по имени в любом шаблоне
    provideAppInitializer(() => inject(FaIconLibrary).addIcons(...APP_ICONS)),

    // До первого экрана пробуем восстановить сессию по cookie
    provideAppInitializer(() => inject(AuthService).refresh()),
  ]
};
