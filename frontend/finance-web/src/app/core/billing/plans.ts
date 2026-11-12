import { SubscriptionPlan } from './billing.service';

export interface PlanDetails {
  plan: SubscriptionPlan;
  name: string;
  // Display only. Stripe prices are the source of truth: keep in sync.
  price: string;
  tagline: string;
  features: string[];
}

// Shared by the public pricing page and the in-app Billing page.
export const PLANS: PlanDetails[] = [
  {
    plan: SubscriptionPlan.Free,
    name: 'Free',
    price: '$0',
    tagline: 'Track everything by hand.',
    features: ['Manual accounts and transactions', 'CSV import', 'Budgets and savings goals', 'Household sharing']
  },
  {
    plan: SubscriptionPlan.Bank,
    name: 'Bank',
    price: '$8.99/month',
    tagline: 'Your bank activity, imported for you.',
    features: ['Everything in Free', 'Automatic bank connections', 'Transactions sync automatically', 'Savings goals that follow an account balance']
  },
  {
    plan: SubscriptionPlan.Premium,
    name: 'Premium',
    price: '$14.99/month',
    tagline: 'Insights on top of your data.',
    features: ['Everything in Bank', 'AI insights and suggestions (coming soon)']
  }
];

// Where users reach support. Replace before launch.
export const SUPPORT_EMAIL = 'support@example.com';
