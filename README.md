# Lending Board

A neighbourhood board for borrowing things instead of buying them
(UN Sustainable Development Goal 12: responsible consumption).

- `backend/LendingBoard.Api`: REST API (ASP.NET Core, EF Core, SQLite)
- `backend/LendingBoard.Tests`: unit tests for the lending rules and integration
  tests for the API (xUnit, `WebApplicationFactory`)
- `frontend`: web page in plain HTML, CSS and JavaScript

## Run it

Backend, on http://localhost:5082:

    dotnet run --project backend/LendingBoard.Api

Frontend, on http://localhost:5173:

    python -m http.server 5173 --directory frontend

The database file `lendingboard.db` is created on first start with demo data.
Delete the file to start fresh. **After updating to 0.2.0 you must delete it once**,
because the `Loans` table has a new column (`DueAt`).

## Tests

    dotnet test

## API

With the backend running, the OpenAPI description is at
http://localhost:5082/openapi/v1.json.

## Diagrams

Database diagram (ERD), UML class diagram and loan life cycle: [docs/uml.md](docs/uml.md).

## Refactoring

What we improved and why: [docs/refactoring.md](docs/refactoring.md).
