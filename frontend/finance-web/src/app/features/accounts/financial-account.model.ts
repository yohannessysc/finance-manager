
export interface FinancialAccount {
  id: string;
  name: string;
  accountType: string;
  currency: string;
  openingBalance: number;
  createdAtUtc: string;
  householdId: string | null;
  isOwner: boolean;
  isBankLinked?: boolean;
}
