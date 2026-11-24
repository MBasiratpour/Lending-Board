# UML

GitHub renders these diagrams directly on this page.

## Database (ERD)

Three tables. Every arrow is a foreign key (FK) pointing to a primary key (PK).

```mermaid
erDiagram
    Members ||--o{ Items : "owns (Items.OwnerId)"
    Members ||--o{ Loans : "borrows (Loans.BorrowerId)"
    Items   ||--o{ Loans : "is lent in (Loans.ItemId)"

    Members {
        int Id PK
        string Name
    }

    Items {
        int Id PK
        string Name
        string Description
        string Category
        int OwnerId FK "-> Members.Id"
    }

    Loans {
        int Id PK
        int ItemId FK "-> Items.Id"
        int BorrowerId FK "-> Members.Id"
        int Status "0 Requested, 1 Approved, 2 Declined, 3 Returned"
        datetime RequestedAt
        datetime DecidedAt "nullable"
        datetime ReturnedAt "nullable"
    }
```

How to read it:

- One member owns zero or more items. Each item has exactly one owner.
- One member borrows zero or more times. Each loan has exactly one borrower.
- One item can be lent zero or more times over its life. Each loan is about exactly one item.
- A loan is linked to `Members` twice: directly as the **borrower**, and through
  the item as the **owner**.
- `Status` is stored as a number. The API sends it as text ("Requested") only in JSON.
- `IsActive` is not a column. It is calculated in C# from `Status`.

## Class diagram

The arrows show which class holds a reference to which (navigation in the code).

```mermaid
classDiagram
    class Member {
        +int Id
        +string Name
    }

    class Item {
        +int Id
        +string Name
        +string Description
        +string Category
        +int OwnerId
        +Member Owner
    }

    class Loan {
        +int Id
        +int ItemId
        +Item Item
        +int BorrowerId
        +Member Borrower
        +LoanStatus Status
        +DateTime RequestedAt
        +DateTime? DecidedAt
        +DateTime? ReturnedAt
        +bool IsActive
        +Request(Item item, int borrowerId, DateTime now)$ Loan
        +Approve(int actingMemberId, DateTime now)
        +Decline(int actingMemberId, DateTime now)
        +MarkReturned(int actingMemberId, DateTime now)
    }

    class LoanStatus {
        <<enumeration>>
        Requested
        Approved
        Declined
        Returned
    }

    Item "*" --> "1" Member : Owner
    Loan "*" --> "1" Item : Item
    Loan "*" --> "1" Member : Borrower
    Loan --> LoanStatus : Status
```

## Life cycle of a loan

```mermaid
stateDiagram-v2
    [*] --> Requested : Request()
    Requested --> Approved : Approve()
    Requested --> Declined : Decline()
    Approved --> Returned : MarkReturned()
    Declined --> [*]
    Returned --> [*]
```

Rules:

- Only the owner of the item may approve, decline or mark a loan as returned.
- A member cannot borrow their own item.
- While a loan is `Requested` or `Approved` (`IsActive`), nobody else can request the item.
