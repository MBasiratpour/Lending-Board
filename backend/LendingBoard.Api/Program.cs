using System.Text.Json.Serialization;
using LendingBoard.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<LendingDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LendingDb")));

// Send enums as text ("Requested") instead of numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The frontend runs on its own port, so the browser needs CORS permission.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LendingDb>();
    db.Database.EnsureCreated();
    db.SeedDemoData();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Turn broken lending rules into a 400 answer with a readable message.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (RuleViolationException e)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = e.Message });
    }
});

// ---------- Members ----------

app.MapGet("/api/members", async (LendingDb db) =>
    await db.Members.OrderBy(m => m.Name).ToListAsync());

app.MapPost("/api/members", async (NewMember input, LendingDb db) =>
{
    var name = (input.Name ?? "").Trim();
    if (name == "")
        throw new RuleViolationException("Please enter a name.");

    var member = new Member { Name = name };
    db.Members.Add(member);
    await db.SaveChangesAsync();
    return Results.Created($"/api/members/{member.Id}", member);
});

// ---------- Items ----------

// Board. Optional filters: ?search=drill&category=Tools&availableOnly=true
app.MapGet("/api/items", async (string? search, string? category, bool? availableOnly, LendingDb db) =>
{
    var query = db.Items.Include(i => i.Owner).AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(i => EF.Functions.Like(i.Name, $"%{search.Trim()}%"));
    if (!string.IsNullOrWhiteSpace(category))
        query = query.Where(i => i.Category == category);

    var items = await query.OrderBy(i => i.Name).ToListAsync();

    // Only the active loans are loaded, and the database does the filtering.
    var activeLoans = await db.Loans.Where(Loan.Active).ToListAsync();
    var now = DateTime.UtcNow;

    var board = items.Select(item =>
    {
        var loan = activeLoans.FirstOrDefault(l => l.ItemId == item.Id);
        return new
        {
            item.Id,
            item.Name,
            item.Description,
            item.Category,
            item.OwnerId,
            OwnerName = item.Owner!.Name,
            // "Available", "Requested" or "Approved" (= on loan)
            Status = loan is null ? "Available" : loan.Status.ToString(),
            DueAt = loan?.DueAt,
            IsOverdue = loan?.IsOverdue(now) ?? false
        };
    });

    return availableOnly == true ? board.Where(i => i.Status == "Available") : board;
});

app.MapPost("/api/items", async (NewItem input, LendingDb db) =>
{
    var name = (input.Name ?? "").Trim();
    if (name == "")
        throw new RuleViolationException("Please enter a name for the item.");
    if (await db.Members.FindAsync(input.OwnerId) is null)
        throw new RuleViolationException("Unknown owner.");

    var category = (input.Category ?? "").Trim();
    var item = new Item
    {
        Name = name,
        Description = (input.Description ?? "").Trim(),
        Category = category == "" ? "Other" : category,
        OwnerId = input.OwnerId
    };
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/items/{item.Id}", new { item.Id });
});

app.MapDelete("/api/items/{id:int}", async (int id, int memberId, LendingDb db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (item.OwnerId != memberId)
        throw new RuleViolationException("Only the owner can remove an item.");

    if (await db.Loans.Where(Loan.Active).AnyAsync(l => l.ItemId == id))
        throw new RuleViolationException("This item has an open request or is on loan.");

    db.Loans.RemoveRange(db.Loans.Where(l => l.ItemId == id));
    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ---------- Loans ----------

app.MapPost("/api/items/{id:int}/loans", async (int id, ActingMember input, LendingDb db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (await db.Members.FindAsync(input.MemberId) is null)
        throw new RuleViolationException("Unknown member.");

    if (await db.Loans.Where(Loan.Active).AnyAsync(l => l.ItemId == id))
        throw new RuleViolationException("This item is not available right now.");

    var loan = Loan.Request(item, input.MemberId, DateTime.UtcNow);
    db.Loans.Add(loan);
    await db.SaveChangesAsync();
    return Results.Created($"/api/loans/{loan.Id}", new { loan.Id });
});

// All loans where the member is the borrower or the owner of the item.
app.MapGet("/api/loans", async (int memberId, LendingDb db) =>
{
    var loans = await db.Loans
        .Include(l => l.Item).ThenInclude(i => i!.Owner)
        .Include(l => l.Borrower)
        .Where(l => l.BorrowerId == memberId || l.Item!.OwnerId == memberId)
        .OrderByDescending(l => l.Id)
        .ToListAsync();

    var now = DateTime.UtcNow;
    return loans.Select(l => new
    {
        l.Id,
        l.ItemId,
        ItemName = l.Item!.Name,
        OwnerId = l.Item.OwnerId,
        OwnerName = l.Item.Owner!.Name,
        l.BorrowerId,
        BorrowerName = l.Borrower!.Name,
        l.Status,
        l.RequestedAt,
        l.DecidedAt,
        l.ReturnedAt,
        l.DueAt,
        IsOverdue = l.IsOverdue(now)
    });
});

// Approve, decline and return all follow the same steps:
// load the loan -> 404 if missing -> change it -> save -> 204.
// Only the middle step differs, so it is passed in as `change`.
// Without a return date, the borrower gets one week.
app.MapPost("/api/loans/{id:int}/approve", (int id, ApproveLoan input, LendingDb db) =>
    ChangeLoan(id, input.MemberId, db,
        (loan, memberId, now) => loan.Approve(memberId, now, input.DueAt ?? now.AddDays(7))));

app.MapPost("/api/loans/{id:int}/decline", (int id, ActingMember input, LendingDb db) =>
    ChangeLoan(id, input.MemberId, db, (loan, memberId, now) => loan.Decline(memberId, now)));

app.MapPost("/api/loans/{id:int}/return", (int id, ActingMember input, LendingDb db) =>
    ChangeLoan(id, input.MemberId, db, (loan, memberId, now) => loan.MarkReturned(memberId, now)));

app.Run();

// ---------- Shared endpoint logic ----------

// Loads a loan with its item, applies one change to it and saves.
// The lending rules themselves stay in the Loan class.
static async Task<IResult> ChangeLoan(
    int id, int memberId, LendingDb db, Action<Loan, int, DateTime> change)
{
    var loan = await db.Loans.Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == id);
    if (loan is null) return Results.NotFound();

    change(loan, memberId, DateTime.UtcNow);
    await db.SaveChangesAsync();
    return Results.NoContent();
}

// ---------- Request bodies ----------

record NewMember(string? Name);
record NewItem(string? Name, string? Description, string? Category, int OwnerId);
record ActingMember(int MemberId);
record ApproveLoan(int MemberId, DateTime? DueAt);

// Lets the integration tests start the API (WebApplicationFactory<Program>).
public partial class Program { }
