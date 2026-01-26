using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scraper.Infrastructure;

public class ScraperDbContextFactory : IDesignTimeDbContextFactory<ScraperDbContext>
{
    public ScraperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScraperDbContext>();
        
        var connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=DataColorApp;Integrated Security=true";
        
        optionsBuilder.UseSqlServer(connectionString);

        return new ScraperDbContext(optionsBuilder.Options);
    }
}
