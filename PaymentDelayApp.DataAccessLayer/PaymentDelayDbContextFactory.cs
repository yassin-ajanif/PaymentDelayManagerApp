using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentDelayApp.DataAccessLayer;

public class PaymentDelayDbContextFactory : IDesignTimeDbContextFactory<PaymentDelayDbContext>
{
    public PaymentDelayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDelayDbContext>()
            .UseSqlite(PaymentDelayDbPaths.BuildConnectionString())
            .Options;
        return new PaymentDelayDbContext(options);
    }
}
