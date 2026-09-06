using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Seeding;

/// <summary>
/// Seeds the initial category list from README §12. Default names are Spanish (the app's neutral
/// culture); resolving a localized display name for a system category at render time is an App
/// layer concern (see the add-localized-text skill), not something baked in here.
/// </summary>
public static class CategorySeeder
{
    public static async Task SeedDefaultCategoriesAsync(TrackTraceMoneyDbContext context, CancellationToken ct = default)
    {
        if (await context.Categories.AnyAsync(ct))
            return;

        var defaults = new[]
        {
            Category.CreateSystemDefined(SystemCategoryKey.Food, "Alimentación"),
            Category.CreateSystemDefined(SystemCategoryKey.Housing, "Vivienda"),
            Category.CreateSystemDefined(SystemCategoryKey.Transportation, "Transporte"),
            Category.CreateSystemDefined(SystemCategoryKey.Health, "Salud"),
            Category.CreateSystemDefined(SystemCategoryKey.Education, "Educación"),
            Category.CreateSystemDefined(SystemCategoryKey.Entertainment, "Entretenimiento"),
            Category.CreateSystemDefined(SystemCategoryKey.Shopping, "Compras"),
            Category.CreateSystemDefined(SystemCategoryKey.Utilities, "Servicios"),
            Category.CreateSystemDefined(SystemCategoryKey.Subscriptions, "Suscripciones"),
            Category.CreateSystemDefined(SystemCategoryKey.Debts, "Deudas"),
            Category.CreateSystemDefined(SystemCategoryKey.Insurance, "Seguros"),
            Category.CreateSystemDefined(SystemCategoryKey.Investments, "Inversiones"),
            Category.CreateSystemDefined(SystemCategoryKey.Savings, "Ahorro"),
            Category.CreateSystemDefined(SystemCategoryKey.Other, "Otros"),
        };

        await context.Categories.AddRangeAsync(defaults, ct);
        await context.SaveChangesAsync(ct);
    }
}
