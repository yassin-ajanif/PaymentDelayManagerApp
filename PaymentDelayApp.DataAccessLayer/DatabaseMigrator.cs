using Microsoft.EntityFrameworkCore;

namespace PaymentDelayApp.DataAccessLayer;

public static class DatabaseMigrator
{
    /// <summary>
    /// Applies EF migrations. If the DB was migrated with a migration that was later removed from the project,
    /// delete the row for that migration from table <c>__EFMigrationsHistory</c> or delete the SQLite file
    /// (<see cref="PaymentDelayDbPaths.DatabaseFilePath"/>) and let <c>InitialCreate</c> recreate the schema.
    /// </summary>
    public static void Migrate(PaymentDelayDbContext db) => db.Database.Migrate();
}
