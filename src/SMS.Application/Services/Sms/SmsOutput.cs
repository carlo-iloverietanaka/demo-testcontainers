namespace SMS.Application.Services.Sms;

public record SmsOutput(
    string ExternalId,
    string Content,
    string ContentType,
    string Sender,
    string Recipient,
    string Medium,
    string Status);