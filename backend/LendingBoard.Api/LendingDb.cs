using Microsoft.EntityFrameworkCore;

namespace LendingBoard.Api;

public class LendingDb(DbContextOptions<LendingDb> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Loan> Loans => Set<Loan>();

    /// <summary>Fills an empty database with a few neighbours and items for demos.</summary>
    public void SeedDemoData()
    {
        if (Members.Any()) return;

        var anna = new Member { Name = "Anna" };
        var ben = new Member { Name = "Ben" };
        var cem = new Member { Name = "Cem" };
        Members.AddRange(anna, ben, cem);

        Items.AddRange(
            new Item { Name = "Cordless drill", Description = "18 V, with a set of bits", Category = "Tools", Owner = anna },
            new Item { Name = "Ladder", Description = "Aluminium, 3 m", Category = "Tools", Owner = ben },
            new Item { Name = "Raclette grill", Description = "For 8 people", Category = "Kitchen", Owner = ben },
            new Item { Name = "Cargo bike", Description = "Carries up to 80 kg", Category = "Transport", Owner = cem });

        SaveChanges();
    }
}
