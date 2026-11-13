# Lending Board

A neighbourhood board for borrowing things instead of buying them
(UN Sustainable Development Goal 12: responsible consumption).

- `backend/LendingBoard.Api`: REST API (ASP.NET Core, EF Core, SQLite)
- `backend/LendingBoard.Tests`: unit tests for the lending rules (xUnit)
- `frontend`: web page in plain HTML, CSS and JavaScript

## Run it

Backend, on http://localhost:5082:

    dotnet run --project backend/LendingBoard.Api

Frontend, on http://localhost:5173:

    python -m http.server 5173 --directory frontend

The database file `lendingboard.db` is created on first start with demo data.
Delete the file to start fresh.

## Tests

    dotnet test

## API

With the backend running, the OpenAPI description is at
http://localhost:5082/openapi/v1.json.

## Diagrams

UML class diagram and loan life cycle: [docs/uml.md](docs/uml.md).
