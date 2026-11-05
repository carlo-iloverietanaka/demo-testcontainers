namespace SMS.Broker.Contracts.Message;

public record SmsPublishing(
    string ExternalId,
    string Content,
    string ContentType,
    string Sender,
    string Recipient,
    string Medium);
