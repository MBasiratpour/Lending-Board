# Refactoring log

Code smells we found in the prototype (v0.1.0), what we did about them,
and which design pattern or principle fits.

| # | Code smell | Where | Refactoring | Pattern / principle | Status |
|---|---|---|---|---|---|
| 1 | **Duplicated code**: approve, decline and return repeat "load loan, 404 if missing, change, save, 204" | `Program.cs`, loan endpoints | Extract one shared method `ChangeLoan` and pass the changing step in as a function | DRY; Template Method idea (fixed steps, one variable step) | Done |
| 2 | **Inefficient query**: `GET /api/items` loads *every* loan into memory, because EF cannot turn the C# property `IsActive` into SQL | `Program.cs`, `GET /api/items` | `Loan.Active` expression, so the database filters (`WHERE Status IN (0, 1)`) | Performance; let the database do the filtering | Done |
| 3 | **Duplicated business rule**: "the item is blocked while a loan is active" was written out in three endpoints | `Program.cs`, board, request loan, delete item | All three use the one rule `Loan.Active` from the domain model | Single source of truth | Done |
| 4 | **God file**: `Program.cs` holds setup, error handling and every endpoint with its logic (about 230 lines) | `Program.cs` | Move logic into `LoanService` / `ItemService`, keep endpoints thin | Service layer; Single Responsibility Principle | Next step |
| 5 | **Magic strings**: the board status `"Available"` is a plain string in the backend and in the frontend | `Program.cs`, `app.js` | Own enum `ItemStatus` (Available, Requested, OnLoan) | Type safety | Defined |
| 6 | **Trusting the client**: the frontend sends `memberId`, so anyone could act as anyone | all write endpoints | Login (e.g. ASP.NET Core Identity / JWT); read the member from the token | Security by design | Defined |

**Measurable effect of #2 (indicator: loan rows loaded per board request)**

| | Before | After |
|---|---|---|
| Loan rows loaded | **all** loans ever (grows forever) | only **active** loans (at most one per item) |
| Example: 4 items, 1,000 old returned loans | 1,000+ rows | at most 4 rows |

## 1. Duplicated code in the loan endpoints (done)

Before, three times almost the same code:

```csharp
app.MapPost("/api/loans/{id:int}/approve", async (int id, ActingMember input, LendingDb db) =>
{
    var loan = await db.Loans.Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == id);
    if (loan is null) return Results.NotFound();
    loan.Approve(input.MemberId, DateTime.UtcNow);   // <- only this line differs
    await db.SaveChangesAsync();
    return Results.NoContent();
});
// ... same for decline and return
```

After, the steps exist once, and each endpoint only says what is different:

```csharp
app.MapPost("/api/loans/{id:int}/approve", (int id, ActingMember input, LendingDb db) =>
    ChangeLoan(id, input, db, (loan, memberId, now) => loan.Approve(memberId, now)));
```

Why it is better:

- A fix (for example logging, or a different error) is made in one place, not three.
- A new action (for example "extend loan") is one line.
- Behaviour is unchanged: the same rules in `Loan` run, and the unit tests still pass.

## 2. and 3. One rule for "active loan", used in the database (done)

Before, the board did this:

```csharp
var activeLoans = (await db.Loans.ToListAsync()).Where(l => l.IsActive).ToList();
//                 ^ loads EVERY loan from the DB, then filters in C#
```

`IsActive` is a normal C# property. Entity Framework cannot translate it to SQL,
so the filter had to run after loading everything.

After, the rule exists as an *expression* that EF can translate:

```csharp
public static readonly Expression<Func<Loan, bool>> Active =
    l => l.Status == LoanStatus.Requested || l.Status == LoanStatus.Approved;

var activeLoans = await db.Loans.Where(Loan.Active).ToListAsync();
// SQL: SELECT ... FROM Loans WHERE Status IN (0, 1)
```

The same `Loan.Active` is now used when a loan is requested and when an item is
deleted, so the rule is written in one place for all database queries.
