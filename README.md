# TrackTrace Money

**English | [Español](README.es.md)**

> **Track. Trace. Control.**
> A mobile app for tracking, monitoring, and analyzing personal finances.

---

## 1. Overview

**TrackTrace Money** will be a mobile app built with **.NET MAUI**, focused on comprehensive personal finance management.

The app will let users record and analyze:

* Income
* Expenses
* Transfers
* Bank accounts
* Savings
* Credit cards
* Loans and credit
* Term deposits
* Investment funds
* Budgets
* Debts
* Net worth
* Medical expenses
* Expenses for others
* Insurance and reimbursements
* Recurring payments
* Reminders
* Financial reports

The main goal won't just be tracking expenses, but **letting the user understand the journey of their money**:

> **Where it comes from → where it sits → what it's used for → what's owed → what's being saved → what's being invested → what's really left.**

---

# 2. System principles

## 2.1 Offline First

The app must work primarily **without an Internet connection**.

All core operations must work offline:

* Create transactions
* Edit transactions
* Delete transactions
* View history
* View accounts
* Record expenses
* Record income
* Record payments
* View debts
* Calculate budgets
* Generate local reports
* Show dashboards
* Create local reminders

An Internet connection **will not be required to use the app**.

---

## 2.2 Local database

The main database will be **SQLite**.

SQLite will be the on-device source of truth, not just a cache.

The following must be accounted for:

* Migrations
* Initial seeds
* Referential integrity
* Transactions
* Indexes
* Local backup
* Restore

---

## 2.3 Optional cloud

The cloud will be a later, optional capability.

Its purpose will be:

* Backup
* Restore
* Synchronization
* Access from multiple devices
* A possible future web app

The app must keep working even if the user never enables cloud services.

---

# 3. Platform and architecture

## 3.1 Technology

### Frontend

* .NET MAUI
* C#
* XAML
* MVVM
* CommunityToolkit.Mvvm
* CommunityToolkit.Maui

### Persistence

* SQLite
* Entity Framework Core
* EF Core SQLite

### Future backend

* ASP.NET Core Web API
* PostgreSQL
* Docker
* Authentication
* Synchronization service

---

# 4. Proposed architecture

```text
TrackTrace Money
│
├── TrackTraceMoney.App
│   ├── Views
│   ├── ViewModels
│   ├── Resources
│   ├── Navigation
│   └── Styles
│
├── TrackTraceMoney.Domain
│   ├── Entities
│   ├── Enums
│   └── Business Rules
│
├── TrackTraceMoney.Application
│   ├── Services
│   ├── Interfaces
│   ├── DTOs
│   └── Use Cases
│
├── TrackTraceMoney.Infrastructure
│   ├── SQLite
│   ├── EF Core
│   ├── Repositories
│   ├── Migrations
│   └── Local Notifications
│
└── TrackTraceMoney.Api       # Future
    ├── Controllers
    ├── Authentication
    ├── Synchronization
    └── PostgreSQL
```

---

# 5. Languages

The app must initially support:

* 🇪🇸 Spanish
* 🇺🇸 English

Localization must be implemented from the start.

UI text must never be hardcoded directly in the views.

Localized resources will be used, for example:

```text
Resources/
├── AppResources.resx
└── AppResources.en.resx
```

The user will be able to change the language from settings.

---

# 6. Currency

The system must allow selecting a primary currency.

Examples:

* USD
* EUR
* MXN
* CRC
* GTQ
* HNL
* NIO
* PAB

Currency must be stored explicitly on accounts and operations where relevant.

---

# 7. People

The system must allow registering people related to expenses.

Example:

```text
Person
├── Name
├── Relationship
└── Notes
```

Predefined relationships:

* Me
* Partner
* Child
* Parent
* Family member
* Other

Custom relationships can also be created.

This will make it possible to distinguish:

> **Who paid** from **who the expense was for**.

---

# 8. Financial accounts

The app must distinguish between different types of financial products.

## 8.1 Asset accounts

* Cash
* Bank account
* Checking account
* Savings account
* Term deposit
* Investment fund

## 8.2 Liability accounts

* Credit card
* Loan
* Bank credit

Conceptually:

```text
FinancialAccount
│
├── Cash
├── BankAccount
├── SavingsAccount
├── TermDeposit
└── InvestmentFund

CreditAccount
│
├── CreditCard
└── Loan
```

---

# 9. Transactions

The system must allow recording different kinds of transactions.

```text
Income
Expense
Transfer
CreditCardPurchase
CreditCardPayment
LoanPayment
InvestmentContribution
InvestmentWithdrawal
InterestIncome
Reimbursement
```

## 9.1 Fundamental rule

Not every transaction represents an expense.

Example:

```text
Bank A → Credit card
$300
```

This represents a **debt payment/transfer**, not a new expense.

The original purchase *was* the expense.

This avoids double-counting expenses.

---

# 10. Income

The user will be able to record:

* Salary
* Bonuses
* Commissions
* Freelance income
* Interest
* Dividends
* Reimbursements
* Other income

Fields:

```text
Amount
Description
Category
Date
Destination account
Person
Notes
```

---

# 11. Expenses

The user will be able to record:

```text
Amount
Description
Category
Date
Payment method
Account
Beneficiary person
Notes
```

Payment methods:

* Cash
* Bank account
* Savings account
* Credit card

Example:

```text
$75.50
Supermarket
09/04/2026
BAC Card
```

---

# 12. Categories

The user will be able to manage categories.

Initial categories:

* Food
* Housing
* Transportation
* Health
* Education
* Entertainment
* Shopping
* Utilities/Services
* Subscriptions
* Debts
* Insurance
* Investments
* Savings
* Other

Categories must be customizable.

---

# 13. Transfers

Funds can be transferred between accounts.

Example:

```text
Bank A
   ↓
$500
   ↓
Savings account
```

A transfer:

* Reduces the source account's balance.
* Increases the destination account's balance.
* Is not counted as an expense.

---

# 14. Credit cards

Each card must store:

```text
Bank / issuer
Name
Last 4 digits
Credit limit
Balance used
Available balance
Annual rate
Monthly rate
Statement/cutoff date
Payment due date
Minimum payment
Pay-in-full amount
Latest statement
Status
```

Example:

```text
💳 BAC Card

Limit:                 $2,000
Used:                    $650
Available:             $1,350

Cutoff:                     25
Payment due:                10

Minimum payment:           $50
Pay-in-full amount:       $500
```

---

# 15. Cycles and statements

Cards must handle billing cycles.

Example:

```text
Cutoff date: 25

August 26
     ↓
September 25
     ↓
Statement
     ↓
October 10
Payment due date
```

It must be possible to identify which purchases belong to each statement.

---

# 16. Card payment

There must be a specific operation:

```text
Card payment

Card:
BAC

Account used:
Main bank

Amount:
$300

Date:
09/10/2026
```

Result:

```text
Bank
-$300

Card
-$300 of debt
```

This must not increase the period's expenses.

---

# 17. "Purchased vs. Paid" analysis

The app must compare:

```text
Card purchases
vs
Payments made
```

Example:

```text
Purchases:   $650
Payments:    $400
----------------
Difference: +$250
```

Indicator:

🟠 **You're spending more than you're paying off.**

If:

```text
Purchases:   $650
Payments:    $800
```

Show:

🟢 **You're paying $150 more than you're spending.**

The analysis can be run by:

* Month
* Cycle
* 3 months
* 6 months
* Year

---

# 18. Card traffic light (semáforo)

Cards must show a visual indicator.

### 🟢 Green

Healthy situation.

Example:

* Pay-in-full amount covered.
* No overdue payments.

### 🟡 Yellow

Attention needed.

Example:

* The due date is approaching.
* The pay-in-full amount hasn't been covered yet.

### 🟠 Orange

At-risk situation.

Example:

* Partial payment.
* High percentage of the limit used.

### 🔴 Red

Critical situation.

Example:

* Overdue payment.
* Obligation still pending after the due date.

The app must distinguish between:

```text
Minimum payment
```

and:

```text
Pay-in-full amount
```

It must never assume each bank's specific interest rules automatically.

The **"pay-in-full amount"** value will be entered by the user from the statement.

---

# 19. Loans and credit

The following can be registered:

* Personal loan
* Auto loan
* Mortgage
* Bank credit
* Installment purchase
* Other loan

Fields:

```text
Institution
Name
Original amount
Current balance
Interest rate
Rate type
Monthly installment
Next payment date
Remaining payments
Required payment
Fees
Notes
```

Example:

```text
🏦 Personal loan

Original:          $5,000
Balance:           $3,250
Rate:                  12%
Installment:         $150
Next payment:  09/15/2026
Remaining:              24
```

---

# 20. Snowball (Bola de Nieve)

The app must include a debt payoff strategy based on the **Snowball** method.

Minimum/obligatory payments must be covered first.

Any extra money is then directed to the debt with the smallest balance.

Example:

```text
Card A          $250
Card B          $800
Card C        $2,500
Loan          $5,000
```

Order:

```text
1. $250
2. $800
3. $2,500
4. $5,000
```

Once a debt is paid off:

```text
Freed-up payment
      ↓
Next debt
```

The system must show:

```text
⭐ CURRENT TARGET

Card A

Balance:       $250
Minimum:        $25
Suggested extra: $150
```

The Snowball strategy will be configurable and must never be forced on the user.

As a future enhancement, the following could also be added:

* Avalanche
* Custom payment

---

# 21. Savings

Accounts dedicated to savings can be registered.

Example:

```text
Account:
Emergency fund

Balance:
$2,500

Goal:
$5,000
```

It can be displayed as:

```text
$2,500 / $5,000
██████████░░░░░░
50%
```

---

# 22. Term deposits

Term deposits must be registrable.

Fields:

```text
Institution
Name
Initial principal
Rate
Rate type
Start date
Maturity date
Interest frequency
Compounding
Estimated interest
Interest received
Auto-renewal
Currency
Notes
```

The app can generate reminders before maturity.

---

# 23. Investment funds

Investments can be registered.

Fields:

```text
Fund
Institution
Investment date
Contributions
Withdrawals
Current value
Return
Fees
Currency
Notes
```

Example:

```text
Initial investment: $2,000
Current value:      $2,084.50

Gain:                  $84.50
Return:                 4.23%
```

A history of the investment's value can be stored to generate charts.

---

# 24. Net worth

The system must calculate net worth.

### Assets

```text
Cash
+ Banks
+ Savings
+ Term deposits
+ Investments
```

### Liabilities

```text
Cards
+ Loans
+ Credit
```

### Formula

```text
Net Worth =
Assets - Liabilities
```

Example:

```text
Assets:       $8,784
Debts:        $3,850
---------------------
Net worth:    $4,934
```

It must be possible to view net worth's evolution over time.

---

# 25. Medical expenses

The app must allow recording expenses related to:

* Doctor
* Dentist
* Medications
* Hospital
* Laboratory
* Therapy
* Other medical services

The expense must allow specifying:

```text
Provider
Patient / beneficiary
Amount
Date
Account used
Payment method
Insurance used
Amount covered
Reimbursable amount
Expected amount
Status
Notes
```

---

# 26. Medical expenses for others

It must be possible to indicate who the expense was made for.

Example:

```text
Patient:
Child

Provider:
Dentist

Cost:
$150

Paid by:
User

Method:
Credit card
```

This makes it possible to separate:

```text
Who paid
```

from:

```text
Who the expense was for
```

---

# 27. Insurance and reimbursements

Expenses can indicate whether they were partially covered by insurance.

Example:

```text
Medical cost:          $150
Insurance:             $100
Final cost:             $50
```

Statuses:

🟡 Reimbursement pending
🟢 Reimbursed
🔴 Rejected

---

# 28. Reimbursements

A reimbursement received must be recorded as **income/recovery linked to the expense**, not as a negative expense.

Example:

```text
Medical expense:
$150

Reimbursement received:
$100

Real cost:
$50
```

An expected reimbursement must not be considered available money until it's actually received.

---

# 29. Insurance paying directly

The following must also be handled:

```text
Medical service:      $200
Insurance pays:        $150
User pays:              $50
```

In this case, the real impact on the user will be:

```text
$50
```

---

# 30. Main dashboard

The main screen must be **simple and visual**.

It must not be cluttered with numbers.

It must quickly answer:

1. How much do I have?
2. How much am I spending?
3. Where am I overspending?
4. What do I need to pay?
5. Do I have a surplus?
6. Is my debt growing or shrinking?
7. Is my net worth growing?

---

# 31. Financial health

The dashboard must show an overall indicator:

```text
MY FINANCIAL HEALTH 🟡
```

States:

🟢 Healthy
🟡 Attention
🟠 At risk
🔴 Critical
🔵 Opportunity

---

# 32. Income and expenses

Example:

```text
INCOME
$1,800

EXPENSES
$1,050

AVAILABLE
$750
```

---

# 33. Over-budget detection

The system must compare spending against the budget.

Example:

```text
Food

Spent:        $420
Budget:       $300

🔴 +$120
```

It can also show the percentage relative to income:

```text
$420
23.3% of your income
```

---

# 34. Budgets

The user will be able to set budgets by:

* Category
* Month
* Frequency

Example:

```text
Food         $300
Transport    $200
Entertain.   $100
```

States:

🟢 Within budget
🟡 Near the limit
🔴 Over budget

---

# 35. Real surplus (sobrante real)

The app must distinguish between:

```text
Available balance
```

and:

```text
Real surplus
```

Example:

```text
Current balance:       $1,000

Upcoming expenses:      -$200
Debt payments:          -$250
--------------------------------
Real surplus:            $550
```

The real surplus is the money left over after accounting for known obligations.

---

# 36. Recommendations for the surplus

When a surplus exists, the app can suggest:

```text
What should you do with your surplus?

⛄ Snowball
🛟 Emergency fund
💰 Savings
📈 Investment
⚖️ Distribute
```

If the user uses the Snowball strategy, it can show:

```text
⭐ You can put an extra $300
toward your target debt.
```

---

# 37. Reminders

The app must use local notifications.

Examples:

```text
🔔 BAC Card

7 days left until payment.
```

```text
🔔 BAC Card

3 days left.
Pay-in-full amount:
$500
```

```text
🔴 BAC Card

Payment is due today.
```

Also:

* Loan payments
* Recurring expenses
* Pending reimbursements
* Term deposit maturities
* Other commitments

All of this must work offline via local notifications.

---

# 38. Recurring expenses

The system must allow configuring recurring transactions:

* Rent
* Internet
* Netflix
* Insurance
* Tuition
* Subscriptions
* Utilities
* Other

Configuration:

```text
Amount
Category
Account
Frequency
Start date
End date
```

---

# 39. History

It must be possible to browse all transactions.

Filters:

* Date
* Category
* Account
* Card
* Person
* Transaction type
* Amount range
* Status

Example:

```text
Sep 04

🛒 Supermarket
-$75.50
BAC Card

Sep 03

💰 Salary
+$1,800
Main bank
```

---

# 40. Reports

Visual, easy-to-read reports must be included.

### Main reports

* Expenses by category
* Daily expenses
* Weekly expenses
* Monthly expenses
* Income vs. expenses
* Budget vs. spending
* Card purchases vs. payments
* Debt evolution
* Snowball
* Net worth
* Medical expenses
* Expenses by person
* Insurance-covered expenses
* Pending reimbursements
* Reimbursements received
* Investment returns
* Surplus evolution

---

# 41. Export

It must be possible to export data.

Planned formats:

* CSV
* Excel
* PDF

Export can be filtered by:

* Period
* Account
* Category
* Transaction type
* Report

---

# 42. Local backup

The app must allow creating local backups.

The backup must include:

* Settings
* Categories
* People
* Accounts
* Transactions
* Cards
* Statements
* Loans
* Investments
* Budgets
* Reminders

Backup encryption should ideally be considered.

---

# 43. Security

The app can optionally be protected using:

* PIN
* Biometrics
* Automatic lock

Local data must be stored securely whenever technically possible.

---

# 44. Navigation

Proposed main navigation:

```text
🏠 Home

💸 Transactions

🏦 My accounts
   ├── Banks
   ├── Savings
   ├── Term deposits
   └── Investment funds

💳 Credit
   ├── Cards
   └── Loans

📊 Reports

🎯 Budgets

🔔 Reminders

⚙️ Settings
   ├── Language
   ├── Currency
   ├── Security
   └── Backup
```

---

# 45. Quick action button

The app must have a `+` button.

Options:

```text
+
├── + Income
├── - Expense
├── ⇄ Transfer
├── 💳 Card payment
├── 🏦 Loan payment
├── 📈 Investment contribution
├── 💰 Investment withdrawal
└── 🔄 Reimbursement
```

---

# 46. Core accounting rules

The system must respect these rules:

### Card purchase

```text
Expense + debt increase
```

### Card payment

```text
Bank decrease
+
Debt decrease
```

It is not a new expense.

### Transfer

```text
Account A -
Account B +
```

Not an expense.

### Investment

```text
Available money -
Investment asset +
```

Must not be automatically treated as an expense.

### Reimbursement

```text
Income/recovery +
```

Must not physically delete the original expense.

---

# 47. Conceptual data model

```text
User
│
├── Person
├── Category
├── FinancialAccount
│
├── Transaction
│
├── CreditCard
│   ├── CreditCardStatement
│   └── CreditCardPayment
│
├── Loan
│
├── Budget
│
├── RecurringTransaction
│
├── MedicalExpense
│   ├── Insurance
│   └── Reimbursement
│
├── TermDeposit
│
├── InvestmentFund
│   └── InvestmentValuation
│
└── Notification
```

---

# 48. Future: synchronization

In a second stage, the following could be implemented:

```text
Mobile
  ↓
API
  ↓
PostgreSQL
```

Synchronization must account for:

* Unique identifiers
* Versioning
* Timestamps
* Conflict resolution
* Pending operations
* Sync status
* Deletions
* Retries

The system must be able to keep working offline and sync later.

---

# 49. Future: web application

A web dashboard could be developed later.

```text
TrackTrace Money Mobile
          │
          ↓
       API
          ↓
     PostgreSQL
          ↑
          │
TrackTrace Money Web
```

The web app could focus mainly on:

* Reports
* Dashboards
* Analysis
* Administration
* Historical queries

---

# 50. Future: artificial intelligence

As a later stage, AI could be added for financial analysis.

Examples:

> "You spent 18% more on food this month."

> "Your card debt has grown over the last three months."

> "You have an estimated surplus of $450 after your obligations."

> "Based on your Snowball strategy, the $250 debt should be your next target."

The AI must operate on the user's data while respecting privacy and security.

---

# 51. Roadmap

## Phase 1 — MVP

* .NET MAUI
* SQLite
* MVVM architecture
* Spanish/English
* Accounts
* Income
* Expenses
* Transfers
* Categories
* History
* Dashboard
* Budgets
* Real surplus
* Recurring expenses
* Local notifications
* Local backup

## Phase 2 — Credit

* Cards
* Cycles
* Statements
* Cutoff dates
* Payment dates
* Minimum payment
* Pay-in-full amount
* Purchases vs. payments
* Traffic light (semáforo)
* Loans
* Snowball

## Phase 3 — Net worth

* Savings
* Term deposits
* Investment funds
* Net worth
* Net worth evolution
* Advanced reports
* Medical expenses
* Insurance
* Reimbursements

## Phase 4 — Cloud

* User account
* API
* PostgreSQL
* Cloud backup
* Restore
* Synchronization

## Phase 5 — Ecosystem

* Multi-device
* Web dashboard
* Financial AI
* Predictive analysis
* Personalized recommendations

---

# 52. Final goal

TrackTrace Money must not simply be:

> **"An app for jotting down expenses."**

It must become:

> **"A tool for understanding the full journey of your money and making better financial decisions."**

The app's central concept will be:

```text
TRACK
↓
Record and monitor

TRACE
↓
Follow the money's journey

CONTROL
↓
Make decisions

GROW
↓
Improve your financial situation
```

### TrackTrace Money

**Track. Trace. Control.**
