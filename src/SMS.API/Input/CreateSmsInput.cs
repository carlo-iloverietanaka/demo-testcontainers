namespace SMS.API.Input;

public record CreateSmsInput(
    string Content,
    string ContentType,
    string Sender,
    string Recipient,
    string Medium);
