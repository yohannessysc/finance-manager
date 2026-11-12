
import { Routes } from '@angular/router';

import { Login } from './features/auth/login/login';

import { AppShell } from './layout/app-shell/app-shell';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'register', loadComponent: () => import('./features/auth/register/register').then(m => m.Register) },
  { path: 'verify-email', loadComponent: () => import('./features/auth/verify-email/verify-email').then(m => m.VerifyEmail) },
  { path: 'confirm-email', loadComponent: () => import('./features/auth/confirm-email/confirm-email').then(m => m.ConfirmEmail) },
  { path: 'confirm-email-change', loadComponent: () => import('./features/auth/confirm-email-change/confirm-email-change').then(m => m.ConfirmEmailChange) },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password/forgot-password').then(m => m.ForgotPassword) },
  { path: 'reset-password', loadComponent: () => import('./features/auth/reset-password/reset-password').then(m => m.ResetPassword) },

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
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard) },
      { path: 'accounts', loadComponent: () => import('./features/accounts/account-list/account-list').then(m => m.AccountList) },
      { path: 'transactions', loadComponent: () => import('./features/transactions/transactions').then(m => m.Transactions) },
      { path: 'recurring', loadComponent: () => import('./features/recurring/recurring').then(m => m.Recurring) },
      { path: 'categories', loadComponent: () => import('./features/categories/categories').then(m => m.Categories) },
      { path: 'budgets', loadComponent: () => import('./features/budgets/budgets').then(m => m.Budgets) },
      { path: 'savings-goals', loadComponent: () => import('./features/savings-goals/savings-goals').then(m => m.SavingsGoals) },
      { path: 'households', loadComponent: () => import('./features/households/households').then(m => m.Households) },
      { path: 'households/accept', loadComponent: () => import('./features/households/accept-household-invitation').then(m => m.AcceptHouseholdInvitation) },
      {
        path: 'bank-connections',
        loadChildren: () => import('./features/bank-connections/bank-connections.routes')
          .then(m => m.BANK_CONNECTIONS_ROUTES)
      },
      { path: 'billing', loadComponent: () => import('./features/billing/billing').then(m => m.Billing) },
      { path: 'settings', loadComponent: () => import('./features/settings/settings').then(m => m.Settings) }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
