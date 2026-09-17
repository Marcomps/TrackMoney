using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.People;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Seeding;

/// <summary>
/// Seeds a single default "Me" person (README §7) so the Payer/Beneficiary pickers on the Expense/
/// CreditCardPurchase flows (<c>AddTransactionViewModel</c>) are useful immediately on a fresh profile,
/// without forcing the user to create themselves as a <see cref="Person"/> first. Mirrors
/// <see cref="CategorySeeder"/>'s own shape exactly (idempotent existence guard, same
/// <see cref="TrackTraceMoneyDbContext"/>/<see cref="TrackTraceMoneyDbContext.SaveChangesAsync"/> call
/// from the same caller — see <see cref="TrackTraceMoney.Infrastructure.Persistence.FinanceDatabaseInitializer"/>).
/// Hardcoded Spanish name rather than a resx-resolved one, for the same reason
/// <see cref="CategorySeeder"/>'s default category names and <c>MauiProgram.cs</c>'s migrated-profile
/// name are hardcoded Spanish: this runs at Infrastructure-layer startup, before any page exists to
/// resolve a UI culture against, and <c>TrackTraceMoney.Infrastructure</c> cannot reference the App
/// project's <c>AppResources</c> (wrong reference direction). The seeded name is a plain, user-editable
/// <see cref="Person.Name"/> the user can rename at any time — unlike a system category's name, it has
/// no enum-driven display converter to translate it at render time.
/// </summary>
public static class PersonSeeder
{
    public static async Task SeedDefaultPeopleAsync(TrackTraceMoneyDbContext context, CancellationToken ct = default)
    {
        if (await context.People.AnyAsync(ct))
            return;

        var me = new Person("Yo", PersonRelationshipType.Me);

        await context.People.AddAsync(me, ct);
        await context.SaveChangesAsync(ct);
    }
}
