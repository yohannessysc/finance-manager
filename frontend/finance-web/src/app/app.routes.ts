
import { Routes } from '@angular/router';

import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { VerifyEmail } from './features/auth/verify-email/verify-email';
import { ConfirmEmail } from './features/auth/confirm-email/confirm-email';
import { ForgotPassword } from './features/auth/forgot-password/forgot-password';
import { ResetPassword } from './features/auth/reset-password/reset-password';

import { Dashboard } from './features/dashboard/dashboard';
import { AccountList } from './features/accounts/account-list/account-list';
import { Transactions } from './features/transactions/transactions';
import { Recurring } from './features/recurring/recurring';
import { Categories } from './features/categories/categories';
import { Settings } from './features/settings/settings';

import { AppShell } from './layout/app-shell/app-shell';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'verify-email', component: VerifyEmail },
  { path: 'confirm-email', component: ConfirmEmail },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'reset-password', component: ResetPassword },

  {
    path: '',
    component: AppShell,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      },
      { path: 'dashboard', component: Dashboard },
      { path: 'accounts', component: AccountList },
      { path: 'transactions', component: Transactions },
      { path: 'recurring', component: Recurring },
      { path: 'categories', component: Categories },
      { path: 'settings', component: Settings }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
