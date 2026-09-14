using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Services;

/// <summary>
/// Android implementation of <see cref="ILocalNotifier"/>. Only concrete platform code lives here —
/// TrackTraceMoney.Infrastructure targets net10.0 with no Android SDK reference, so this can't live
/// there (see the task's architecture note / CLAUDE.md's solution layout).
///
/// Upholds the "never throws" contract documented on <see cref="ILocalNotifier"/>: every failure path
/// (permission denied, missing manifest permission, any platform exception) is caught here so a
/// notification problem can never break an expense save that already succeeded.
/// </summary>
public sealed class AndroidLocalNotifier : ILocalNotifier
{
    private const string ChannelId = "budget_exceeded";
    private const string TermDepositRenewedChannelId = "term_deposit_renewed";
    private static volatile bool _channelCreated;
    private static volatile bool _termDepositRenewedChannelCreated;

    public async Task NotifyBudgetExceededAsync(
        Category category,
        decimal budgetAmount,
        decimal amountOver,
        CancellationToken ct = default)
    {
        try
        {
            if (Android.App.Application.Context is not { } context)
                return;

            EnsureChannel(context);

            if (!await EnsurePermissionAsync())
                return;

            var categoryName = SystemCategoryKeyToLabelConverter.GetDisplayName(category);
            var title = string.Format(AppResources.Notification_BudgetExceeded_Title, categoryName);
            var body = string.Format(
                AppResources.Notification_BudgetExceeded_Body,
                categoryName,
                amountOver.ToString("N2"),
                budgetAmount.ToString("N2"));

            // Reuses the launcher icon as the small icon rather than a dedicated monochrome
            // notification asset (Android best practice) — adding a new icon resource is outside this
            // slice's scope; flagged for a future polish pass.
            var smallIconId = context.ApplicationInfo?.Icon ?? 0;

            var builder = new NotificationCompat.Builder(context, ChannelId);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetSmallIcon(smallIconId);
            builder.SetAutoCancel(true);
            builder.SetPriority(NotificationCompat.PriorityDefault);

            // One notification per category: a later crossing for the same category replaces the
            // previous one rather than stacking (no notification history/inbox is in scope here).
            var notification = builder.Build();
            var manager = NotificationManagerCompat.From(context);
            if (notification is not null && manager is not null)
                manager.Notify(category.Id.GetHashCode(), notification);
        }
        catch
        {
            // Swallow — see the "never throws" contract on ILocalNotifier.
        }
    }

    public async Task NotifyTermDepositRenewedAsync(
        string institution,
        decimal renewedAmount,
        CurrencyCode currency,
        DateOnly newMaturityDate,
        CancellationToken ct = default)
    {
        try
        {
            if (Android.App.Application.Context is not { } context)
                return;

            EnsureTermDepositRenewedChannel(context);

            if (!await EnsurePermissionAsync())
                return;

            var title = string.Format(AppResources.Notification_TermDepositRenewed_Title, institution);
            var body = string.Format(
                AppResources.Notification_TermDepositRenewed_Body,
                institution,
                renewedAmount.ToString("N2"),
                currency,
                newMaturityDate.ToString("d"));

            // Reuses the launcher icon as the small icon rather than a dedicated monochrome
            // notification asset, matching NotifyBudgetExceededAsync's precedent.
            var smallIconId = context.ApplicationInfo?.Icon ?? 0;

            var builder = new NotificationCompat.Builder(context, TermDepositRenewedChannelId);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetSmallIcon(smallIconId);
            builder.SetAutoCancel(true);
            builder.SetPriority(NotificationCompat.PriorityDefault);

            // One notification per (institution, new maturity date) pair — no dedicated entity id is
            // passed through this interface, so the id is derived from the same fields shown in the
            // notification body, mirroring NotifyBudgetExceededAsync's category.Id.GetHashCode() idiom.
            var notificationId = HashCode.Combine(institution, newMaturityDate);

            var notification = builder.Build();
            var manager = NotificationManagerCompat.From(context);
            if (notification is not null && manager is not null)
                manager.Notify(notificationId, notification);
        }
        catch
        {
            // Swallow — see the "never throws" contract on ILocalNotifier.
        }
    }

    private static void EnsureChannel(Context context)
    {
        if (_channelCreated || !OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = NotificationManager.FromContext(context);
        var channel = new NotificationChannel(
            ChannelId,
            AppResources.Notification_BudgetExceeded_ChannelName,
            NotificationImportance.Default)
        {
            Description = AppResources.Notification_BudgetExceeded_ChannelDescription
        };

        manager?.CreateNotificationChannel(channel);
        _channelCreated = true;
    }

    private static void EnsureTermDepositRenewedChannel(Context context)
    {
        if (_termDepositRenewedChannelCreated || !OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = NotificationManager.FromContext(context);
        var channel = new NotificationChannel(
            TermDepositRenewedChannelId,
            AppResources.Notification_TermDepositRenewed_ChannelName,
            NotificationImportance.Default)
        {
            Description = AppResources.Notification_TermDepositRenewed_ChannelDescription
        };

        manager?.CreateNotificationChannel(channel);
        _termDepositRenewedChannelCreated = true;
    }

    private static async Task<bool> EnsurePermissionAsync()
    {
        // POST_NOTIFICATIONS only exists from API 33 (Tiramisu) onward; older devices show
        // notifications without a runtime prompt.
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return true;

        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();

        return status == PermissionStatus.Granted;
    }
}
