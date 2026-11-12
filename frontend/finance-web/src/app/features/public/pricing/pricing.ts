import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PLANS } from '../../../core/billing/plans';

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './pricing.html',
  styleUrl: '../public-pages.scss'
})
export class Pricing {
  readonly plans = PLANS;
}
