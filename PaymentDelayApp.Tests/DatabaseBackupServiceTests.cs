using Microsoft.Data.Sqlite;
using Moq;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class DatabaseBackupServiceTests
{
    private static DatabaseBackupService CreateSut()
    {
        var invoices = new Mock<IInvoiceService>();
        invoices.Setup(x => x.GetInvoicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Invoice>());
        var suppliers = new Mock<ISupplierService>();
        suppliers.Setup(x => x.GetSuppliersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Supplier>());
        var export = new Mock<IInvoiceDashboardExportService>();
        export.Setup(x => x.WriteExcelAsync(
                It.IsAny<IReadOnlyList<InvoiceDashboardRow>>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var supplierExcel = new Mock<ISupplierExcelService>();
        supplierExcel.Setup(x => x.WriteExcelAsync(
                It.IsAny<IReadOnlyList<Supplier>>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new DatabaseBackupService(
            invoices.Object,
            suppliers.Object,
            export.Object,
            supplierExcel.Object);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), "PaymentDelayApp.Tests.DbBackup." + Guid.NewGuid().ToString("N"));

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static async Task<string> CreateSourceSqliteFileAsync()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "source.db");
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE t(x INTEGER PRIMARY KEY); INSERT INTO t(x) VALUES (42);";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        return dbPath;
    }

    [TestMethod]
    public async Task CreateBackupAsync_SourceMissing_ThrowsFileNotFoundException()
    {
        var sut = CreateSut();
        var backupDir = CreateTempDir();
        Directory.CreateDirectory(backupDir);
        try
        {
            var missing = Path.Combine(backupDir, "nope.db");
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
                sut.CreateBackupAsync(missing, backupDir, BackupSettingsFile.DefaultRetentionDays, CancellationToken.None)).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(backupDir);
        }
    }

    [TestMethod]
    public async Task CreateBackupAsync_NullOrWhiteSpacePaths_ThrowArgumentException()
    {
        var sut = CreateSut();
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                sut.CreateBackupAsync("", dir, 10, CancellationToken.None)).ConfigureAwait(false);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                sut.CreateBackupAsync(" ", dir, 10, CancellationToken.None)).ConfigureAwait(false);
            var src = Path.Combine(dir, "s.db");
            await File.WriteAllTextAsync(src, "").ConfigureAwait(false);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                sut.CreateBackupAsync(src, "", 10, CancellationToken.None)).ConfigureAwait(false);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                sut.CreateBackupAsync(src, "   ", 10, CancellationToken.None)).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [TestMethod]
    public async Task CreateBackupAsync_WritesBackupFileAndCopiesData()
    {
        var sut = CreateSut();
        var sourcePath = await CreateSourceSqliteFileAsync().ConfigureAwait(false);
        var sourceDir = Path.GetDirectoryName(sourcePath)!;
        var backupRoot = CreateTempDir();
        try
        {
            await sut.CreateBackupAsync(sourcePath, backupRoot, 30, CancellationToken.None).ConfigureAwait(false);
            var backups = Directory.GetFiles(backupRoot, "app_backup_*.db", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, backups.Length);
            var backupCs = new SqliteConnectionStringBuilder { DataSource = backups[0] }.ToString();
            await using var read = new SqliteConnection(backupCs);
            await read.OpenAsync().ConfigureAwait(false);
            await using var cmd = read.CreateCommand();
            cmd.CommandText = "SELECT x FROM t;";
            var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            Assert.AreEqual(42L, Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            TryDeleteDirectory(backupRoot);
            TryDeleteDirectory(sourceDir);
        }
    }

    [TestMethod]
    public async Task CreateBackupAsync_PreCancelledToken_DoesNotWriteBackup()
    {
        var sut = CreateSut();
        var sourcePath = await CreateSourceSqliteFileAsync().ConfigureAwait(false);
        var sourceDir = Path.GetDirectoryName(sourcePath)!;
        var backupRoot = CreateTempDir();
        try
        {
            var ct = new CancellationToken(true);
            // Sqlite OpenAsync typically throws TaskCanceledException (subclass of OperationCanceledException).
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
                sut.CreateBackupAsync(sourcePath, backupRoot, 30, ct)).ConfigureAwait(false);
            Assert.AreEqual(0, Directory.GetFiles(backupRoot, "app_backup_*.db", SearchOption.TopDirectoryOnly).Length);
        }
        finally
        {
            TryDeleteDirectory(backupRoot);
            TryDeleteDirectory(sourceDir);
        }
    }

    [TestMethod]
    public async Task PruneBackupsAsync_MissingDirectory_CompletesWithoutThrow()
    {
        var sut = CreateSut();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "_no_such_backup_dir");
        await sut.PruneBackupsAsync(missing, 10, CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task PruneBackupsAsync_NullOrWhiteSpaceDirectory_ThrowsArgumentException()
    {
        var sut = CreateSut();
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            sut.PruneBackupsAsync("", 10, CancellationToken.None)).ConfigureAwait(false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            sut.PruneBackupsAsync(" ", 10, CancellationToken.None)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task PruneBackupsAsync_DeletesOldAppBackupFilesOnly()
    {
        var sut = CreateSut();
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        var oldBackup = Path.Combine(dir, "app_backup_old.db");
        var recentBackup = Path.Combine(dir, "app_backup_recent.db");
        var other = Path.Combine(dir, "other.db");
        await File.WriteAllTextAsync(oldBackup, "").ConfigureAwait(false);
        await File.WriteAllTextAsync(recentBackup, "").ConfigureAwait(false);
        await File.WriteAllTextAsync(other, "").ConfigureAwait(false);
        File.SetLastWriteTimeUtc(oldBackup, DateTime.UtcNow.AddDays(-60));
        File.SetLastWriteTimeUtc(recentBackup, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-60));
        try
        {
            await sut.PruneBackupsAsync(dir, 30, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(File.Exists(oldBackup));
            Assert.IsTrue(File.Exists(recentBackup));
            Assert.IsTrue(File.Exists(other));
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [TestMethod]
    public async Task PruneBackupsAsync_RetentionDaysClampedToMinimum()
    {
        var sut = CreateSut();
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "app_backup_stale.db");
        await File.WriteAllTextAsync(path, "").ConfigureAwait(false);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-3));
        try
        {
            await sut.PruneBackupsAsync(dir, 0, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [TestMethod]
    public async Task PruneBackupsAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var sut = CreateSut();
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "app_backup_x.db");
        await File.WriteAllTextAsync(path, "").ConfigureAwait(false);
        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                sut.PruneBackupsAsync(dir, 30, cts.Token)).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [TestMethod]
    public async Task CreateBackupAsync_ThenPruneRemovesStaleBackups()
    {
        var sut = CreateSut();
        var sourcePath = await CreateSourceSqliteFileAsync().ConfigureAwait(false);
        var sourceDir = Path.GetDirectoryName(sourcePath)!;
        var backupRoot = CreateTempDir();
        Directory.CreateDirectory(backupRoot);
        var stale = Path.Combine(backupRoot, "app_backup_stale.db");
        await File.WriteAllTextAsync(stale, "").ConfigureAwait(false);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-90));
        try
        {
            await sut.CreateBackupAsync(sourcePath, backupRoot, 30, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(File.Exists(stale));
            Assert.IsTrue(Directory.GetFiles(backupRoot, "app_backup_*.db", SearchOption.TopDirectoryOnly).Length >= 1);
        }
        finally
        {
            TryDeleteDirectory(backupRoot);
            TryDeleteDirectory(sourceDir);
        }
    }
}
