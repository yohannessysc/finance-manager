import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing.html',
  styleUrl: '../public-pages.scss'
})
export class Landing {
  readonly features = [
    {
      title: 'Every account in one place',
      text: 'Chequing, savings and credit cards with live balances and a cash-flow forecast.'
    },
    {
      title: 'Budgets that keep up',
      text: 'Monthly limits by category, aligned to the day your pay arrives.'
    },
    {
      title: 'Savings goals with a plan',
      text: 'See what to save each month, whether you are on track, and when you will get there.'
    },
    {
      title: 'Automatic bank sync',
      text: 'Connect your bank on the Bank plan and new transactions arrive on their own.'
    },
    {
      title: 'Built for households',
      text: 'Share accounts, budgets and goals with a partner, with roles for who can edit.'
    },
    {
      title: 'Private by design',
      text: 'Bank access tokens are encrypted, two-step verification is available, and you can export or delete your data any time.'
    }
  ];
}
