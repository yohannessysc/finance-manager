
import { ApplicationConfig } from '@angular/core';

import {
  provideBrowserGlobalErrorListeners
} from '@angular/core';

import { provideRouter } from '@angular/router';

import {
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';

import { routes } from './app.routes';

import {
  csrfInterceptor
} from './core/auth/csrf.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideRouter(routes),

    provideHttpClient(
      withInterceptors([
        csrfInterceptor
      ])
    )
  ]
};
