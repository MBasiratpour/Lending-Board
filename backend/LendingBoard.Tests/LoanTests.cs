using LendingBoard.Api;

namespace LendingBoard.Tests;

public class LoanTests
{
    private const int OwnerId = 1;
    private const int BorrowerId = 2;
    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private static Item Drill() => new() { Id = 7, Name = "Drill", OwnerId = OwnerId };

    private static Loan RequestedLoan() => Loan.Request(Drill(), BorrowerId, Now);

    [Fact]
    public void Request_creates_an_open_request_for_the_item()
    {
        var loan = RequestedLoan();

        Assert.Equal(LoanStatus.Requested, loan.Status);
        Assert.Equal(7, loan.ItemId);
        Assert.Equal(BorrowerId, loan.BorrowerId);
        Assert.Equal(Now, loan.RequestedAt);
        Assert.True(loan.IsActive);
    }

    [Fact]
    public void Owner_cannot_borrow_their_own_item()
    {
        Assert.Throws<RuleViolationException>(() => Loan.Request(Drill(), OwnerId, Now));
    }

    [Fact]
    public void Owner_can_approve_an_open_request()
    {
        var loan = RequestedLoan();

        loan.Approve(OwnerId, Now.AddHours(1), Now.AddDays(7));

        Assert.Equal(LoanStatus.Approved, loan.Status);
        Assert.Equal(Now.AddHours(1), loan.DecidedAt);
        Assert.Equal(Now.AddDays(7), loan.DueAt);
        Assert.True(loan.IsActive);
    }

    [Fact]
    public void Borrower_cannot_approve_their_own_request()
    {
        var loan = RequestedLoan();

        Assert.Throws<RuleViolationException>(() => loan.Approve(BorrowerId, Now, Now.AddDays(7)));
        Assert.Equal(LoanStatus.Requested, loan.Status);
    }

    [Fact]
    public void Declined_request_no_longer_blocks_the_item()
    {
        var loan = RequestedLoan();

        loan.Decline(OwnerId, Now);

        Assert.Equal(LoanStatus.Declined, loan.Status);
        Assert.False(loan.IsActive);
    }

    [Fact]
    public void Request_cannot_be_approved_twice()
    {
        var loan = RequestedLoan();
        loan.Approve(OwnerId, Now, Now.AddDays(7));

        Assert.Throws<RuleViolationException>(() => loan.Approve(OwnerId, Now, Now.AddDays(7)));
    }

    [Fact]
    public void Item_can_only_be_returned_after_approval()
    {
        var loan = RequestedLoan();

        Assert.Throws<RuleViolationException>(() => loan.MarkReturned(OwnerId, Now));
    }

    [Fact]
    public void Returned_loan_frees_the_item()
    {
        var loan = RequestedLoan();
        loan.Approve(OwnerId, Now, Now.AddDays(7));

        loan.MarkReturned(OwnerId, Now.AddDays(2));

        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.Equal(Now.AddDays(2), loan.ReturnedAt);
        Assert.False(loan.IsActive);
    }

    // ---------- Due date and overdue (new in 0.2.0) ----------

    [Fact]
    public void Return_date_must_be_in_the_future()
    {
        var loan = RequestedLoan();

        Assert.Throws<RuleViolationException>(() => loan.Approve(OwnerId, Now, Now.AddDays(-1)));
        Assert.Equal(LoanStatus.Requested, loan.Status);
    }

    [Fact]
    public void Loan_is_not_overdue_before_the_return_date()
    {
        var loan = RequestedLoan();
        loan.Approve(OwnerId, Now, Now.AddDays(7));

        Assert.False(loan.IsOverdue(Now.AddDays(6)));
    }

    [Fact]
    public void Loan_is_overdue_after_the_return_date()
    {
        var loan = RequestedLoan();
        loan.Approve(OwnerId, Now, Now.AddDays(7));

        Assert.True(loan.IsOverdue(Now.AddDays(8)));
    }

    [Fact]
    public void Returned_loan_is_never_overdue()
    {
        var loan = RequestedLoan();
        loan.Approve(OwnerId, Now, Now.AddDays(7));
        loan.MarkReturned(OwnerId, Now.AddDays(10));

        Assert.False(loan.IsOverdue(Now.AddDays(11)));
    }
}
