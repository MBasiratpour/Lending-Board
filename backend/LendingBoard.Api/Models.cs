using System.Linq.Expressions;

namespace LendingBoard.Api;

/// <summary>A neighbour who can offer and borrow items.</summary>
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Something a member offers to lend, e.g. a drill.</summary>
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";

    public int OwnerId { get; set; }
    public Member? Owner { get; set; }
}

public enum LoanStatus
{
    Requested,
    Approved,
    Declined,
    Returned
}

/// <summary>Thrown when an action breaks a lending rule. The API answers with 400.</summary>
public class RuleViolationException(string message) : Exception(message);

/// <summary>
/// One borrowing of one item. Life cycle:
/// Requested -> Approved -> Returned, or Requested -> Declined.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item? Item { get; set; }

    public int BorrowerId { get; set; }
    public Member? Borrower { get; set; }

    public LoanStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }

    /// <summary>The day the borrower promised to bring the item back. Set when the owner approves.</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>A loan blocks the item while it is waiting for an answer or out of the house.</summary>
    public bool IsActive => Status is LoanStatus.Requested or LoanStatus.Approved;

    /// <summary>
    /// The same rule as <see cref="IsActive"/>, written as an expression so that
    /// Entity Framework can turn it into SQL (WHERE Status IN (0, 1)).
    /// Use it in database queries: db.Loans.Where(Loan.Active)
    /// </summary>
    public static readonly Expression<Func<Loan, bool>> Active =
        l => l.Status == LoanStatus.Requested || l.Status == LoanStatus.Approved;

    /// <summary>The item is still out of the house after the agreed return date.</summary>
    public bool IsOverdue(DateTime now) => Status == LoanStatus.Approved && DueAt < now;

    public static Loan Request(Item item, int borrowerId, DateTime now)
    {
        if (item.OwnerId == borrowerId)
            throw new RuleViolationException("You cannot borrow your own item.");

        return new Loan
        {
            ItemId = item.Id,
            Item = item,
            BorrowerId = borrowerId,
            Status = LoanStatus.Requested,
            RequestedAt = now
        };
    }

    public void Approve(int actingMemberId, DateTime now, DateTime dueAt)
    {
        RequireOwner(actingMemberId);
        RequireStatus(LoanStatus.Requested, "Only an open request can be approved.");
        if (dueAt <= now)
            throw new RuleViolationException("The return date must be in the future.");

        Status = LoanStatus.Approved;
        DecidedAt = now;
        DueAt = dueAt;
    }

    public void Decline(int actingMemberId, DateTime now)
    {
        RequireOwner(actingMemberId);
        RequireStatus(LoanStatus.Requested, "Only an open request can be declined.");
        Status = LoanStatus.Declined;
        DecidedAt = now;
    }

    public void MarkReturned(int actingMemberId, DateTime now)
    {
        RequireOwner(actingMemberId);
        RequireStatus(LoanStatus.Approved, "Only an item that is on loan can be returned.");
        Status = LoanStatus.Returned;
        ReturnedAt = now;
    }

    private void RequireOwner(int actingMemberId)
    {
        if (Item is null)
            throw new InvalidOperationException("The loan's item must be loaded.");
        if (Item.OwnerId != actingMemberId)
            throw new RuleViolationException("Only the owner of the item can do this.");
    }

    private void RequireStatus(LoanStatus expected, string message)
    {
        if (Status != expected)
            throw new RuleViolationException(message);
    }
}
