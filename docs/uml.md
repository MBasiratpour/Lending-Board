# UML

GitHub renders these diagrams directly on this page.

## Class diagram

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
    }

    class Loan {
        +int Id
        +int ItemId
        +int BorrowerId
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

    Member "1" --> "*" Item : owns
    Member "1" --> "*" Loan : borrows
    Item "1" --> "*" Loan : is lent in
    Loan --> LoanStatus
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
