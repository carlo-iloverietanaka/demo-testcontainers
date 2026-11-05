using SMS.Application.Output;

namespace SMS.Application.Services.Sms;

public interface ISmsService
{
    Task<Yield> FindByExternalId(string externalId);
    Task<Yield> FindPendingThenLease();
    Task<int> LeaseToPublish(IEnumerable<string> externalIds);
    Task<int> LeaseToPending(IEnumerable<string> externalIds);
    Task<Yield> CreateSms(SmsInput input);
    Task<Yield> UpdateSmsStatus(SmsInputStatus inputStatus);
    Task<int> DeleteSms(string externalId);
}