using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Api.Persistence;

namespace TrackTraceMoney.Api.Backups;

/// <summary>
/// Phase 4 slice 6: cloud backup/restore for the whole-file SQLite backup already produced
/// on-device by <c>TrackTraceMoney.Infrastructure</c>'s <c>LocalBackupService</c>. One opaque
/// backup blob per user, upload always overwrites — no versioning/history, no multi-device
/// conflict handling. See this slice's spec before extending this file.
/// </summary>
public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backup").RequireAuthorization();

        group.MapPost("/", UploadAsync);
        group.MapGet("/", DownloadAsync);
        group.MapGet("/status", GetStatusAsync);
    }

    private static async Task<Results<Ok<BackupStatusResponse>, BadRequest>> UploadAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        TrackTraceMoneyCloudDbContext dbContext,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream, cancellationToken);
        var data = memoryStream.ToArray();

        if (data.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        var userId = GetUserId(user);

        var existing = await dbContext.Backups
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            existing.ReplaceData(data);
        }
        else
        {
            existing = new CloudBackup(userId, data);
            dbContext.Backups.Add(existing);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Narrow race window: another concurrent upload for the same user won the insert
            // between our read above and this SaveChanges, tripping the unique index on UserId.
            // A single retry is sufficient here — re-fetch the row the other request just
            // created and overwrite it, matching this endpoint's documented "upload always
            // overwrites" behavior instead of surfacing a bare 500.
            dbContext.Entry(existing).State = EntityState.Detached;

            existing = await dbContext.Backups
                .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Expected a concurrently-inserted backup row after a unique-constraint conflict, but none was found.");

            existing.ReplaceData(data);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new BackupStatusResponse(true, existing.UpdatedAtUtc, existing.SizeBytes));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> DownloadAsync(
        ClaimsPrincipal user,
        TrackTraceMoneyCloudDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        var backup = await dbContext.Backups
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (backup is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(backup.Data, "application/octet-stream", "tracktracemoney-backup.db3");
    }

    private static async Task<Ok<BackupStatusResponse>> GetStatusAsync(
        ClaimsPrincipal user,
        TrackTraceMoneyCloudDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        // Deliberately projects straight to BackupStatusResponse — never materializes the Data
        // column here, that would be wasteful for a status-only check.
        var status = await dbContext.Backups
            .Where(b => b.UserId == userId)
            .Select(b => new BackupStatusResponse(true, b.UpdatedAtUtc, b.SizeBytes))
            .FirstOrDefaultAsync(cancellationToken);

        return TypedResults.Ok(status ?? new BackupStatusResponse(false, null, null));
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
