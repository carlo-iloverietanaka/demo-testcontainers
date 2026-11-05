using Microsoft.AspNetCore.Http;
using SMS.Application.ExternalServices.Persistence;
using SMS.Application.Output;

namespace SMS.Application.Services.Sms;

public class SmsService : ISmsService
{
    private readonly IQuerySms _querySms;

    public SmsService(IQuerySms querySms)
    {
        _querySms = querySms;
    }

    public async Task<Yield> FindByExternalId(string externalId)
    {
        if (externalId is "" or null)
        {
            return new YieldError(StatusCodes.Status400BadRequest,
                "BadRequest",
                "ExternalId is required");
        }

        var sms = await _querySms.FindByExternalIdAsync(externalId);

        if (sms is null)
        {
            return new YieldError(StatusCodes.Status404NotFound,
                "NotFound",
                "Sms not found");
        }

        return new YieldOk<SmsOutput>(StatusCodes.Status200OK, new SmsOutput(
            sms.ExternalId,
            sms.Content,
            sms.ContentType,
            sms.Sender,
            sms.Recipient,
            sms.Medium ?? "",
            sms.Status ?? ""));
    }

    public async Task<Yield> FindPendingThenLease()
    {
        var smsPending = await _querySms.FindsPendingAsync();
        var smsOutputs = new List<SmsOutput>();

        if (smsPending.Count > 0)
        {
            smsOutputs =
            [
                .. smsPending.Select(x => new SmsOutput(
                    x.ExternalId,
                    x.Content,
                    x.ContentType,
                    x.Sender,
                    x.Recipient,
                    x.Medium ?? "",
                    x.Status ?? ""))
            ];
        }

        return new YieldOk<List<SmsOutput>>(StatusCodes.Status200OK, smsOutputs);
    }
    
    public Task<int> LeaseToPublish(IEnumerable<string> externalIds)
    {
        return _querySms.LeaseToPublishedAsync(externalIds);
    }
    
    public Task<int> LeaseToPending(IEnumerable<string> externalIds)
    {
        return _querySms.LeaseToPendingAsync(externalIds);
    }

    public async Task<Yield> CreateSms(SmsInput input)
    {
        var validator = new SmsValidator();
        var validatorResult = await validator.ValidateAsync(input);

        if (validatorResult.IsValid is false)
        {
            return new YieldError(StatusCodes.Status400BadRequest,
                "BadRequest",
                validatorResult.Errors[0].ErrorMessage);
        }

        var sms = new Domain.Entities.Sms
        {
            Id = 0,
            ExternalId = null!,
            Content = input.Content,
            ContentType = input.ContentType,
            Sender = input.Sender,
            Recipient = input.Recipient,
            Medium = input.Medium,
            Status = nameof(SmsStatus.Pending),
            CreatedAt = DateTime.UtcNow
        };

        var smsCreated = await _querySms.InsertAsync(sms);

        return new YieldOk<SmsOutput>(StatusCodes.Status200OK, new SmsOutput(
            smsCreated.ExternalId,
            smsCreated.Content,
            smsCreated.ContentType,
            smsCreated.Sender,
            smsCreated.Recipient,
            smsCreated.Medium ?? "",
            smsCreated.Status ?? ""));
    }

    public async Task<Yield> UpdateSmsStatus(SmsInputStatus inputStatus)
    {
        if (inputStatus.ExternalId is "" or null)
        {
            return new YieldError(StatusCodes.Status400BadRequest,
                "BadRequest",
                "ExternalId is required");
        }

        var sms = await _querySms.FindByExternalIdAsync(inputStatus.ExternalId);

        if (sms is null)
        {
            return new YieldError(StatusCodes.Status404NotFound,
                "NotFound",
                "Sms not found");
        }

        sms.Status = inputStatus.Status.ToString();

        switch (inputStatus.Status)
        {
            case SmsStatus.Published:
                sms.PublishedAt = DateTime.UtcNow;
                break;
            case SmsStatus.Delivered or SmsStatus.Failed:
                sms.CompletedAt = DateTime.UtcNow;
                break;
        }

        var smsUpdated = await _querySms.UpdateAsync(sms);

        return new YieldOk<SmsOutput>(StatusCodes.Status200OK, new SmsOutput(
            smsUpdated.ExternalId,
            smsUpdated.Content,
            smsUpdated.ContentType,
            smsUpdated.Sender,
            smsUpdated.Recipient,
            smsUpdated.Medium ?? "",
            smsUpdated.Status ?? ""));
    }

    public Task<int> DeleteSms(string externalId)
    {
        return _querySms.DeleteByExternalIdAsync(externalId);
    }
}