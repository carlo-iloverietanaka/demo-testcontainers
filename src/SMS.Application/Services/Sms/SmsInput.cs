namespace SMS.Application.Services.Sms;

public record SmsInput(
    string Content,
    string ContentType,
    string Sender,
    string Recipient,
    string Medium);