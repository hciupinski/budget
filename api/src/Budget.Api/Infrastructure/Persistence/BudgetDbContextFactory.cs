using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDbContextFactory : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BudgetDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=budget;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new BudgetDbContext(optionsBuilder.Options);
    }
}
