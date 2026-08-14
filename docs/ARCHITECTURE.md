# STOKVEL COOPERATIVE — System Architecture

## 1. System Architecture

Layered .NET 10 backend + React (Vite) SPA.

```
React SPA (client)
    └── JWT REST API (Stokvel.Api)
            ├── Stokvel.Application  (use cases, DTOs, rules)
            ├── Stokvel.Infrastructure (EF Core, Identity, ledger persistence)
            └── Stokvel.Domain (entities, enums, invariants)
```

- **API:** ASP.NET Core Web API, JWT Bearer, CORS for the SPA.
- **Data:** SQLite (dev-ready). Ledger balances are **derived**, never stored as editable fields.
- **Auth:** ASP.NET Core Identity + JWT. First-run setup creates `PLATFORM_ADMIN`. No default password in source.
- **Isolation:** Every group-scoped query filters by `GroupId`. Platform Admin bypasses group filters.

## 2. Database ERD (logical)

```
User ──< GroupMember >── StokvelGroup ── GroupRule
  │                         │
  │                         ├── GroupInvitation
  │                         ├── FinancialAccount ──< FinancialTransaction
  │                         ├── ContributionSchedule ──< Contribution
  │                         ├── GroupLoan ──< GroupLoanRepayment / GroupLoanApproval
  │                         └── MemberLoan ──< MemberLoanRepayment / MemberLoanApproval
  │
  ├── Notification
  └── AuditLog
```

## 3–4. Tables & Relationships

See Domain entities. Key FKs: `StokvelGroup.Id` on almost all financial tables; `GroupMember.Id` on member-level money; `FinancialTransaction` never hard-deleted.

## 5. Role & Permission Matrix

| Capability | Platform Admin | Stokvel Admin | Treasurer | Chairperson | Secretary | Member |
|---|---|---|---|---|---|---|
| View all groups | ✓ | | | | | |
| Approve / activate / suspend groups | ✓ | | | | | |
| Manage own group | | ✓ | | | ✓ (members) | |
| Record deposits / repayments | | ✓ | ✓ | | | own only |
| Request group loan | | ✓ | | ✓ | | |
| Approve group/member loans | ✓ | ✓* | ✓* | ✓* | | |
| Request member loan | | ✓ | ✓ | ✓ | ✓ | ✓ |
| Approve own loan | never | never | never | never | never | never |

\*according to the group’s configured approval workflow.

## 6. Business Rules (enforced in Application)

R1 Min 5 members before activation.  
R2 Configured initial deposit must be confirmed.  
R3 Monthly instalment rule required.  
R4 Monthly obligations auto-created.  
R5 Only **Confirmed** transactions affect balance.  
R6 Group cannot borrow above configured % of savings.  
R7 Member cannot borrow above eligibility (e.g. 2× contributions).  
R8 Loan cannot be disbursed before approval.  
R9 User cannot approve their own loan.  
R10 Closed stokvel cannot create loans.  
R11 Financial transactions cannot be permanently deleted (reversal/adjustment only).  
R12 Every financial event is auditable.  
R13 Users only access authorised stokvels.  
R14 Platform Admin can view all stokvels.

## 7. Group Creation Workflow

Register → Create Stokvel (Draft / Awaiting Members) → Invite members → 5 members → Pending Approval → Platform Admin activates → Awaiting Initial Deposit → Deposit confirmed → **Active**.

## 8. Initial Deposit Workflow

Group Admin configures amount → Record deposit (Pending) → Treasurer/Admin confirms → Ledger credit (Group Capital) → Status Active (if other requirements met).

## 9. Monthly Instalment Workflow

Job/on-demand generator creates `ContributionSchedule` per month (group + each member) → Member/Admin records payment → Confirm → Ledger credit → Status Paid / Partial / Overdue + optional penalty.

## 10. Group Borrowing Workflow

Eligibility check → Draft application → Submit → Approval steps (configurable) → Platform Admin final (if required) → Disburse (ledger outflow + receivable) → Active.

## 11. Member Borrowing Workflow

Request → Eligibility → Group approval (not self) → Disburse from group capital → Repayment schedule.

## 12. Loan Repayment Workflow

Schedule generated at disbursement → Record repayment → Confirm → Ledger inflow (repayment) + reduce outstanding.

## 13. Financial Ledger Design

Accounts: GroupCapital, MemberContribution, LoanReceivable, CooperativePool.  
Balance(account) = Σ confirmed inflows − Σ confirmed outflows.  
Posting always inside a database transaction.

## 14–16. Dashboards

Platform / Group / Member dashboards consume dedicated summary endpoints (no client-side balance math).

## 17. API Architecture

REST `/api/{resource}` with JWT. Setup, Auth, Groups, Members, Contributions, Ledger, Loans, Reports, Notifications, Audit, Settings.

## 18. Security Architecture

JWT, Identity lockout, password policy, group-scope authorization handler, no permanent financial deletes, audit log on sensitive actions.

## 19. Notification Architecture

In-app `Notification` rows created by application services on state changes.

## 20. Reporting Architecture

Server-side aggregation from the ledger + loan tables. CSV/JSON via report endpoints.
