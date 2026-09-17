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
/// Unlike <see cref="CategorySeeder"/>'s default category names (which resolve through a live
/// culture-aware converter keyed off <c>SystemCategoryKey</c> at render time, so the stored literal
/// never actually reaches the screen), <see cref="Person.Name"/> has no such converter — it renders
/// exactly as stored, everywhere. <c>TrackTraceMoney.Infrastructure</c> cannot reference the App
/// project's <c>AppResources</c> directly (wrong reference direction), so <paramref name="name"/> is
/// supplied by the caller instead, which is in the App layer and can pass an already-localized string
/// (typically <c>AppResources.PersonRelationshipType_Me</c> — same label already used for
/// <see cref="PersonRelationshipType.Me"/> everywhere else it's displayed).
/// </summary>
public static class PersonSeeder
{
    public static async Task SeedDefaultPeopleAsync(TrackTraceMoneyDbContext context, string name, CancellationToken ct = default)
    {
        if (await context.People.AnyAsync(ct))
            return;

        var me = new Person(name, PersonRelationshipType.Me);

        await context.People.AddAsync(me, ct);
        await context.SaveChangesAsync(ct);
    }
}
