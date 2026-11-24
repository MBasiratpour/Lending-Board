# Release notes

## 0.2.0 (MVP for the Technical Review)

New features:

- **Return date and overdue loans.** The owner picks a return date when approving
  (default: one week). Loans that are not back in time get a red "Overdue" badge
  on the board and in "My loans". The demo data contains one overdue loan.
- **Search and filter the board** by name, category and "only available".
  The API supports `GET /api/items?search=&category=&availableOnly=`.

Refactoring (no change in behaviour):

- Approve, decline and return share one method instead of three copies.
- The board asks the database only for active loans instead of loading all loans.
- The rule "a loan is active" exists once (`Loan.Active`) and is used everywhere.

Quality:

- Unit tests for the return date and overdue rules (12 unit tests in total).
- Integration tests that start the real API with a temporary SQLite database (5 tests).
- Both run automatically in the GitHub Actions pipeline.

Docs:

- Database diagram (ERD) with primary and foreign keys; fixed class diagram.
- Refactoring log in `docs/refactoring.md`.

**Upgrade note:** the `Loans` table has a new column `DueAt`. Delete
`lendingboard.db` once so it is created again with the new column and demo data.

## 0.1.0 (prototype)

- Board with all items the neighbours offer and their status.
- Offer an item and remove your own items.
- Ask to borrow an item; the owner approves or declines and marks it as returned.
- "My loans" view for requests on your items and things you asked for.
- Demo data (three neighbours, four items) on first start.
