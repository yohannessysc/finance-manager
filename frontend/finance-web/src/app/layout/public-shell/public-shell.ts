import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { SUPPORT_EMAIL } from '../../core/billing/plans';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './public-shell.html',
  styleUrl: './public-shell.scss'
})
export class PublicShell {
  readonly supportEmail = SUPPORT_EMAIL;
  readonly year = new Date().getFullYear();
}
