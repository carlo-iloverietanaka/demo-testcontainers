using SMS.Web.Services;

namespace SMS.Web.Tracking;

public sealed record SmsStatusChange(string Status, DateTime At);

public enum StepState
{
    Todo,
    Current,
    Done,

    // Reached, but between two polls, so it was never observed.
    Passed,

    // Never reached because the SMS failed.
    Skipped
}

public sealed class TrackedSms
{
    private readonly List<SmsStatusChange> _history = [];

    public required string ExternalId { get; init; }
    public required DateTime TrackedAt { get; init; }

    // False when the SMS was looked up by external id instead of sent from this page.
    public required bool SentHere { get; init; }

    public string Sender { get; private set; } = "";
    public string Recipient { get; private set; } = "";
    public string Medium { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public string Content { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string? Error { get; set; }
    public DateTime CheckedAt { get; set; }

    public IReadOnlyList<SmsStatusChange> History => _history;

    public DateTime StatusSince => _history.Count > 0 ? _history[^1].At : TrackedAt;

    public bool IsFinal => SmsStages.IsFinal(Status);

    public void Apply(SmsOutput sms, DateTime now)
    {
        Sender = sms.Sender;
        Recipient = sms.Recipient;
        Medium = sms.Medium;
        ContentType = sms.ContentType;
        Content = sms.Content;
        Status = sms.Status;
        Error = null;
        CheckedAt = now;

        if (_history.Count is 0 || _history[^1].Status != sms.Status)
        {
            _history.Add(new SmsStatusChange(sms.Status, now));
        }
    }

    public DateTime? SeenAt(string status)
    {
        return _history.FirstOrDefault(x => x.Status == status)?.At;
    }

    public StepState StateOf(int flowIndex)
    {
        var seen = SeenAt(SmsStages.Flow[flowIndex].Status) is not null;

        if (Status is SmsStages.Failed)
        {
            return seen ? StepState.Done : StepState.Skipped;
        }

        var current = SmsStages.FlowIndex(Status);

        if (current < 0 || flowIndex > current)
        {
            return StepState.Todo;
        }

        if (flowIndex < current)
        {
            return seen ? StepState.Done : StepState.Passed;
        }

        return IsFinal ? StepState.Done : StepState.Current;
    }
}
