# STOKVEL COOPERATIVE

A multi-group stokvel and cooperative finance platform.

- **Backend:** ASP.NET Core 10 Web API (`src/Stokvel.Api`)
- **Frontend:** React + Vite (`client`)
- **Data:** SQLite ledger (balances are calculated, never edited by hand)

Architecture notes: `docs/ARCHITECTURE.md`

## Run the platform

Terminal 1 — API:

```powershell
cd src/Stokvel.Api
dotnet run --launch-profile http
```

API: http://localhost:5184  
Swagger: http://localhost:5184/swagger

Terminal 2 — React app:

```powershell
cd client
npm install
npm run dev
```

App: http://localhost:5173

## First-run setup

The first visit creates the **Platform Administrator**. There is no default password in source code.

Use a password with at least 10 characters, including upper case, lower case, a digit and a symbol.

## Typical flow

1. Register as a user (or sign in as Platform Admin).
2. Create a Stokvel — the creator becomes Group Administrator.
3. Invite members until there are **5**.
4. Platform Admin approves the group.
5. Record and confirm the **initial deposit**.
6. Generate monthly instalments and confirm payments.
7. Request group or member loans, approve (not self), disburse, then repay.

## Roles

| Role | Access |
|---|---|
| Platform Administrator | All Stokvels, approvals, platform reports |
| Stokvel Administrator | Own group only |
| Treasurer / Chairperson / Secretary | Group operations for their role |
| Member | Own contributions, loans and statements |

Groups cannot see each other's financial records.
