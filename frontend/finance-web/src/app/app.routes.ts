
import { Routes } from '@angular/router';

import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { AccountList } from './features/accounts/account-list/account-list';

import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'accounts'
  },
  {
    path: 'login',
    component: Login
  },
  {
    path: 'register',
    component: Register
  },
  {
    path: 'accounts',
    component: AccountList,
    canActivate: [authGuard]
  },
  {
    path: '**',
    redirectTo: 'accounts'
  }
];
