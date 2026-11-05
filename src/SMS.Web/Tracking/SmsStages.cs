using MudBlazor;

namespace SMS.Web.Tracking;

public sealed record SmsStage(string Status, string Owner, string Where, string Icon, Color Color, string CssColor);

public static class SmsStages
{
    public const string Failed = "Failed";

    // The happy path an SMS walks through, in order.
    public static readonly SmsStage[] Flow =
    [
        new("Pending", "SMS.API → Postgres",
            "Saved by SMS.API. Waiting for SMS.Publishing to claim it, which happens once a minute.",
            Icons.Material.Filled.Schedule, Color.Default, "var(--mud-palette-text-secondary)"),
        new("Publishing", "SMS.Publishing → RabbitMQ",
            "Claimed by SMS.Publishing and queued on sms.publishing for SMS.Sender.",
            Icons.Material.Filled.CloudUpload, Color.Warning, "var(--mud-palette-warning)"),
        new("Published", "SMS.Sender",
            "Sent by SMS.Sender and reported back on sms.published.",
            Icons.Material.Filled.Send, Color.Info, "var(--mud-palette-info)"),
        new("Delivered", "Provider",
            "Confirmed delivered by the provider. Nothing reports this yet.",
            Icons.Material.Filled.DoneAll, Color.Success, "var(--mud-palette-success)")
    ];

    public static readonly SmsStage FailedStage = new(Failed, "Any step after Pending",
        "Reported as failed by SMS.Sender.",
        Icons.Material.Filled.ErrorOutline, Color.Error, "var(--mud-palette-error)");

    public static readonly SmsStage[] All = [.. Flow, FailedStage];

    // Published is final for polling because nothing reports Delivered yet; a manual refresh still checks it.
    private static readonly HashSet<string> FinalStatuses = ["Published", "Delivered", Failed];

    public static bool IsFinal(string status)
    {
        return FinalStatuses.Contains(status);
    }

    public static int FlowIndex(string status)
    {
        return Array.FindIndex(Flow, x => x.Status == status);
    }

    public static SmsStage? Find(string status)
    {
        return Array.Find(All, x => x.Status == status);
    }

    public static string Duration(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        }

        return elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
            : $"{elapsed.Seconds}s";
    }
}
