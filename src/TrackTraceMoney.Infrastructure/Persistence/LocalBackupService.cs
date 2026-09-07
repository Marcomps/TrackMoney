using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// Backs up and restores the on-device SQLite database (README §42) by copying the underlying
/// database file wholesale, rather than re-serializing entities. The whole Phase 1 dataset
/// (categories, people, accounts, transactions, budgets) already lives in that one file, so a
/// file copy is an exact, schema-drift-proof backup.
/// </summary>
/// <remarks>
/// <para>
/// <b>Connection-lifetime investigation (read before changing restore behavior):</b>
/// This app's DI setup (<c>MauiProgram.cs</c> / <c>InfrastructureServiceCollectionExtensions.cs</c>)
/// registers <see cref="TrackTraceMoneyDbContext"/> and the repositories with the default EF Core
/// Scoped lifetime, but nothing in the app ever creates a per-page or per-operation
/// <c>IServiceScope</c> (the only <c>CreateScope()</c> call is the one-time migration/seed block in
/// <c>MauiProgram.CreateMauiApp</c>). MAUI's Shell resolves Views/ViewModels via
/// <c>DataTemplate</c> against the app's root <see cref="IServiceProvider"/>. Resolving a Scoped
/// service directly from a root container (rather than from a child scope) makes that root
/// container behave as the "scope" for caching purposes — so in practice there is a single,
/// long-lived <see cref="TrackTraceMoneyDbContext"/> instance (and therefore a single EF Core
/// change tracker / identity map) for the entire app session, shared by every page.
/// </para>
/// <para>
/// That does NOT mean the raw ADO.NET connection is held open continuously: EF Core's SQLite
/// provider opens the underlying <see cref="SqliteConnection"/> implicitly around each individual
/// operation and closes it again afterwards (unless a caller explicitly calls
/// <c>Database.OpenConnection()</c> or starts an explicit transaction, which this app never does).
/// However, <c>Microsoft.Data.Sqlite</c> pools native connections by default, so an EF-Core-"closed"
/// connection can still correspond to an open, pooled OS file handle sitting idle between
/// operations. <see cref="SqliteConnection.ClearAllPools"/> forces those idle pooled handles to be
/// released, which is what makes a raw file copy/overwrite safe on Windows (where an open handle
/// without delete/write sharing could otherwise block it) and prevents a stale pooled connection
/// from being handed back after we've replaced the file underneath it.
/// </para>
/// <para>
/// Clearing the pool is necessary but not sufficient for a safe *live* restore, though. Because the
/// app keeps a single long-lived <see cref="TrackTraceMoneyDbContext"/> for its whole session, its
/// change tracker may still hold entities loaded from the *old* file. Even after the file is
/// swapped and the pool is cleared, further queries against that same context could hand back
/// stale tracked entities via EF Core's identity resolution instead of the newly restored data, for
/// any primary key that happens to already be tracked. Simply reopening the connection would not
/// fix that. The MVP-safe answer implemented here is: perform the file overwrite, then require the
/// app to be closed and reopened — the App-layer Settings screen surfaces that requirement to the
/// user via a resx-driven message. This intentionally does not attempt a live in-session recovery.
/// </para>
/// <para>
/// This class does not "fix" the shared-root-DbContext DI setup itself (e.g. by introducing
/// per-page scopes) — that is a broader change with app-wide implications outside this feature's
/// scope, and is flagged separately rather than changed as a drive-by here.
/// </para>
/// </remarks>
public sealed class LocalBackupService : ILocalBackupService
{
    private static readonly byte[] SqliteHeaderMagic = "SQLite format 3\0"u8.ToArray();

    private readonly TrackTraceMoneyDbContext _dbContext;

    public LocalBackupService(TrackTraceMoneyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Stream> ExportAsync(CancellationToken ct = default)
    {
        var dbPath = GetDatabasePath();

        // Release any idle pooled native connections before reading the file — see class remarks.
        SqliteConnection.ClearAllPools();

        var bytes = await File.ReadAllBytesAsync(dbPath, ct);
        return new MemoryStream(bytes, writable: false);
    }

    public async Task RestoreAsync(Stream backupData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(backupData);

        var dbPath = GetDatabasePath();
        var tempPath = dbPath + ".restore-tmp";

        try
        {
            await using (var tempFile = File.Create(tempPath))
            {
                await backupData.CopyToAsync(tempFile, ct);
            }

            await ValidateSqliteFileAsync(tempPath, ct);

            // Release any idle pooled native connections before touching the live file on disk —
            // see class remarks for why this alone doesn't make the change visible mid-session.
            SqliteConnection.ClearAllPools();

            File.Copy(tempPath, dbPath, overwrite: true);

            // Drop any leftover rollback-journal/WAL sidecar files from the *previous* database so a
            // stale journal can never be replayed against the freshly restored main file.
            foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
            {
                var sidecar = dbPath + suffix;
                if (File.Exists(sidecar))
                {
                    File.Delete(sidecar);
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task ValidateSqliteFileAsync(string path, CancellationToken ct)
    {
        var header = new byte[SqliteHeaderMagic.Length];

        await using (var stream = File.OpenRead(path))
        {
            var read = await stream.ReadAsync(header, ct);
            if (read < header.Length || !header.AsSpan().SequenceEqual(SqliteHeaderMagic))
            {
                throw new InvalidDataException("The selected file is not a valid TrackTrace Money backup.");
            }
        }
    }

    private string GetDatabasePath()
    {
        var connectionString = _dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The database connection string is not available.");

        return new SqliteConnectionStringBuilder(connectionString).DataSource;
    }
}
