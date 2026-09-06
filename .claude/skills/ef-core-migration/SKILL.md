---
name: ef-core-migration
description: Add or apply an EF Core migration for TrackTraceMoney's SQLite DbContext (in TrackTraceMoney.Infrastructure). Use whenever an entity, DbContext configuration, or the schema changes and a migration needs to be created or applied.
---

# EF Core migrations for TrackTraceMoney

The database is SQLite, owned by `TrackTraceMoney.Infrastructure` (`Microsoft.EntityFrameworkCore.Sqlite` + `.Design` packages are already referenced there). The MAUI app (`TrackTraceMoney.App`) is **Android-only** (`net10.0-android`), which `dotnet-ef` cannot use as a startup project — it's not a runnable .NET host. Use `TrackTraceMoney.Infrastructure` as both the project and the startup project instead.

## One-time setup (if not already present)

Check whether a design-time factory exists before assuming you need one:

```
Grep "IDesignTimeDbContextFactory" src/TrackTraceMoney.Infrastructure
```

If missing, add one next to the `DbContext` class, e.g.:

```csharp
public class TrackTraceMoneyDbContextFactory : IDesignTimeDbContextFactory<TrackTraceMoneyDbContext>
{
    public TrackTraceMoneyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrackTraceMoneyDbContext>()
            .UseSqlite("Data Source=design_time.db")
            .Options;
        return new TrackTraceMoneyDbContext(options);
    }
}
```

The connection string here is only for design-time tooling (generating migrations) — it is never used at runtime. The real runtime connection string points at the app's local data directory (`FileSystem.AppDataDirectory` on the device) and is wired up in `MauiProgram.cs`/DI registration, not here.

Ensure the `dotnet-ef` tool is available:

```
dotnet tool list --global
# if dotnet-ef is missing:
dotnet tool install --global dotnet-ef
```

## Adding a migration

```
dotnet ef migrations add <DescriptiveName> \
  --project src/TrackTraceMoney.Infrastructure \
  --startup-project src/TrackTraceMoney.Infrastructure \
  --output-dir Migrations
```

Review the generated migration before committing:
- Is it additive (new table/column/index) rather than destructive (dropped/renamed column, changed non-nullable-with-no-default)? Destructive migrations run against a device that may already hold a real user's data — there's no "just restore from server" fallback in an offline-first app.
- Do new foreign keys/indices match the referential integrity and index expectations from README §2.2 and §47.

## Applying a migration

There is no server to run `dotnet ef database update` against in production. Two contexts:

- **Local dev/testing** (e.g. verifying the migration applies cleanly against a throwaway file): `dotnet ef database update --project src/TrackTraceMoney.Infrastructure --startup-project src/TrackTraceMoney.Infrastructure`.
- **On-device (real behavior)**: the app calls `dbContext.Database.Migrate()` during startup (`MauiProgram.cs` / app initialization), against the device's actual local SQLite file. This is what ships — the CLI command above is only for verifying the migration in isolation before it reaches that code path.

## After generating a migration

Run `dotnet build` on the whole solution to confirm nothing else broke, and mention in your summary whether the migration is additive or destructive so the `infra-architect` reviewer knows what to check for.
