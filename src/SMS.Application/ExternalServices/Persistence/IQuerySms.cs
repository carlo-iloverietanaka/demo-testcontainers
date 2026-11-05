using SMS.Domain.Entities;

namespace SMS.Application.ExternalServices.Persistence;

public interface IQuerySms
{
    Task<Sms?> FindByIdAsync(int id, CancellationToken token = default);
    Task<Sms?> FindByExternalIdAsync(string externalId, CancellationToken token = default);
    Task<List<Sms>> FindsPendingAsync(CancellationToken token = default);
    Task<int> LeaseToPublishedAsync(IEnumerable<string> externalIds, CancellationToken token = default);
    Task<int> LeaseToPendingAsync(IEnumerable<string> externalIds, CancellationToken token = default);
    Task<Sms> InsertAsync(Sms sms, CancellationToken token = default);
    Task<Sms> UpdateAsync(Sms sms, CancellationToken token = default);
    Task<int> DeleteByIdAsync(int id, CancellationToken token = default);
    Task<int> DeleteByExternalIdAsync(string externalId, CancellationToken token = default);
}