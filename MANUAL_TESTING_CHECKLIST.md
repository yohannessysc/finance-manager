# Manual Testing Checklist - Pre-Bank Integration

Complete this checklist to verify all features work correctly before bank integration.

## Phase 1: Authentication & Account Management ✅

### Login & Session
- [ ] Login with correct credentials → Dashboard loads
- [ ] Login with wrong password → Error message shown
- [ ] Email not verified → Special error message
- [ ] Account locked after failed attempts → Locked error
- [ ] Logout → Redirects to login, session cleared
- [ ] Session timeout after 30 mins → Redirects to login
- [ ] Multiple sessions per user work independently

### Password Management
- [ ] Forgot password → Email sent with reset link
- [ ] Reset password link works → Can set new password
- [ ] Old password no longer works
- [ ] Can change password from settings
- [ ] Password change invalidates other sessions

### Email Verification
- [ ] Unverified account cannot log in
- [ ] "Resend verification" email link works
- [ ] Clicking verification link confirms email
- [ ] Can now log in after verification

### Account Security
- [ ] View active sessions in settings
- [ ] Revoke specific sessions
- [ ] "Sign out all other sessions" works
- [ ] Activity shows device name and UserAgent
- [ ] Two-factor authentication (if implemented)

### Privacy & Data Export
- [ ] Download all personal data as JSON/CSV
- [ ] Export includes all financial records
- [ ] Delete account option visible (with warning)
- [ ] Deletion removes all user data

---

## Phase 2: Financial Management Features

### 1. Budgets by Category ✅
- [ ] Create budget with category and monthly limit
- [ ] Budget shows in dashboard with current spending
- [ ] Multiple budgets per category can exist
- [ ] Edit budget: change limit, dates, category
- [ ] Delete budget (with confirmation)
- [ ] View budget detail:
  - [ ] Category breakdown
  - [ ] Current month spending vs limit
  - [ ] Previous months spending history
  - [ ] Visual progress bar (red if over, green if under)
- [ ] Filter budgets by date range
- [ ] Budget periods use correct dates (1st to last day of month)
- [ ] Budget period labels show start and end dates clearly

### 2. Savings Goals ✅
- [ ] Create goal: name, target amount, deadline
- [ ] Goal shows progress (amount saved / target)
- [ ] Visual progress indicator (percentage)
- [ ] Edit goal: change amount, deadline, description
- [ ] Mark goal as completed
- [ ] Delete goal
- [ ] Filter goals by status (Active, Completed, Past Due)
- [ ] Goals sorted by deadline (soonest first)
- [ ] View goal detail:
  - [ ] Monthly savings needed calculation
  - [ ] Time remaining
  - [ ] Historical contributions

### 3. Cash Flow Forecasting ✅
- [ ] View cash flow forecast chart (next 90 days)
- [ ] Chart shows:
  - [ ] Starting balance
  - [ ] Income line (recurring income)
  - [ ] Expense line (bills + transactions)
  - [ ] Net balance projection
- [ ] Forecast updates when income/expenses change
- [ ] Can adjust forecast parameters:
  - [ ] Include/exclude specific recurring transactions
  - [ ] Change start date
- [ ] Forecast warns if balance goes negative

### 4. CSV Import ✅
- [ ] Upload CSV file with transactions
- [ ] Preview shows mapped columns
- [ ] Select which columns map to (date, amount, description, category)
- [ ] Duplicate detection:
  - [ ] Shows transactions that already exist
  - [ ] Allows skipping or overwriting duplicates
  - [ ] Fingerprinting prevents re-import of same batch
- [ ] Import completes without errors
- [ ] Imported transactions appear in accounts
- [ ] Bank/source column preserved in transaction data

### 5. Household Sharing ✅
- [ ] Create household (multi-account group)
- [ ] Invite user by email
- [ ] Invited user receives email
- [ ] Accept/decline invitation
- [ ] Member list shows all household users
- [ ] Remove member from household
- [ ] View member roles (Owner, Admin, Member)

### 6. Shared Budgets & Permissions ✅
- [ ] Share account with household member:
  - [ ] Toggle share status
  - [ ] Confirm share dialog
  - [ ] Share removes from "My Accounts", appears in "Shared"
- [ ] Shared account accessible to household members
- [ ] Member can only VIEW shared account:
  - [ ] See transactions
  - [ ] See balances
  - [ ] Cannot edit/delete (read-only)
- [ ] Owner can still edit shared account
- [ ] Budget permissions:
  - [ ] Owner can create budgets on shared account
  - [ ] Members can view budgets
  - [ ] Members cannot create/edit budgets
- [ ] Account switching works:
  - [ ] "My Accounts" vs "Shared Accounts" tabs
  - [ ] Dashboard updates when switching account
  - [ ] Transactions/budgets show correct account data

### 7. Financial Accounts ✅
- [ ] Create financial account (name, type, balance)
- [ ] Account appears in list
- [ ] Edit account: name, type, opening balance
- [ ] Delete account (with confirmation)
- [ ] Account types:
  - [ ] Checking/Savings/Credit Card/Investment/Loan
  - [ ] Each type displays appropriately
- [ ] Account balance:
  - [ ] Manual balance entry
  - [ ] Calculated from transactions
  - [ ] Matches sum of debits/credits

### 8. Financial Transactions ✅
- [ ] Create transaction:
  - [ ] Manual entry (date, amount, description, category)
  - [ ] Immediate or recurring option
  - [ ] Balance updates immediately
- [ ] Transaction list shows:
  - [ ] Date, description, amount, category
  - [ ] Running balance
  - [ ] Sorting by date (newest first)
- [ ] Edit transaction
- [ ] Delete transaction (with confirmation)
- [ ] Filter transactions:
  - [ ] By date range
  - [ ] By category
  - [ ] By amount (income vs expense)
- [ ] Recurring transactions:
  - [ ] Set frequency (daily, weekly, monthly, yearly)
  - [ ] Automatically post on schedule
  - [ ] Can view all instances
  - [ ] Can skip/modify specific occurrence

### 9. Categories ✅
- [ ] View category list (with colors/icons)
- [ ] Create custom category
- [ ] Edit category name and icon
- [ ] Delete category (reassign transactions)
- [ ] Standard categories pre-populated

### 10. Reporting & Dashboards ✅
- [ ] Dashboard summary shows:
  - [ ] Total net worth
  - [ ] Monthly income
  - [ ] Monthly expenses
  - [ ] Savings rate percentage
  - [ ] Active budgets status
  - [ ] Goal progress
- [ ] Spending by category chart:
  - [ ] Pie chart or bar chart
  - [ ] Current month vs previous months
  - [ ] Clickable to drill into category
- [ ] Income vs Expenses chart:
  - [ ] Monthly comparison over last 12 months
  - [ ] Clear trend indication
- [ ] Account balance chart:
  - [ ] Historical balance over time
  - [ ] Per account or combined
- [ ] Cash flow summary:
  - [ ] Income total
  - [ ] Expense total
  - [ ] Net cash flow
- [ ] Financial health score (if applicable)
- [ ] Export reports as PDF

---

## Settings & Preferences ✅

- [ ] Preferred currency setting
- [ ] Date format preference
- [ ] Theme (light/dark mode)
- [ ] Notification preferences
- [ ] Account privacy settings
- [ ] All settings persist across sessions

---

## Performance & Stability ✅

- [ ] Dashboard loads in < 2 seconds
- [ ] Large data sets (1000+ transactions) don't hang
- [ ] Concurrent budget/account updates work
- [ ] No memory leaks or continuous growth
- [ ] Backend tests pass: `dotnet test backend`
- [ ] Frontend builds: `ng build`

---

## Data Integrity ✅

- [ ] Transaction calculations are accurate
- [ ] Budget sums match individual transactions
- [ ] Household account permissions enforced
- [ ] Session validation prevents unauthorized access
- [ ] Database migrations clean (no pending changes)
- [ ] No orphaned records in database

---

## Testing Strategy

### Quick Test (15 mins)
1. Login → Dashboard shows
2. Create account, add transactions
3. Create budget, verify it calculates correctly
4. View cash flow forecast
5. Logout and login again (session persists)

### Comprehensive Test (1 hour)
1. Complete all auth tests (Phase 1)
2. Test all financial features with sample data
3. Test household sharing with multiple users
4. Test CSV import with duplicates
5. View all reports and dashboards
6. Test settings persistence

### Stress Test (30 mins)
1. Import 500+ transactions via CSV
2. Create 20+ budgets and goals
3. Navigate dashboard with large data sets
4. Check for performance degradation

---

## Sign-Off

When all items are ✅ checked:
- Product is ready for bank integration
- All core financial management features working
- Privacy and security verified
- Performance acceptable
- Data integrity maintained

**Date Tested:** _______________
**Tested By:** _______________
**Issues Found:** _______________

