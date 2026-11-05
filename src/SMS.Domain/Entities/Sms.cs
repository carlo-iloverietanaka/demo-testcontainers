namespace SMS.Domain.Entities;

public class Sms
{
    public required int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Content { get; set; }
    public required string ContentType { get; set; }
    public required string Sender { get; set; }
    public required string Recipient { get; set; }

    /// <summary>
    /// The medium on which the SMS message was sent.
    /// </summary>
    public string? Medium { get; set; }

    /// <summary>
    /// Possible status of the SMS message.
    /// </summary>
    /// <remarks>
    /// - Pending
    /// - Publishing
    /// - Published
    /// - Delivered
    /// - Failed
    /// </remarks>
    public string? Status { get; set; }

    /// <summary>
    /// The date and time when the SMS message was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the SMS message was published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// The date and time when the SMS message was completed.
    /// This is populated when the message is delivered or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The date and time until a publishing worker holds the claim on the SMS message.
    /// Once expired another worker can claim it again, it is cleared when the broker confirmed the message.
    /// </summary>
    public DateTime? LockedUntil { get; set; }
}