using Dapper;
using Npgsql;
using SMS.Application.ExternalServices.Persistence;
using SMS.Domain.Entities;

namespace SMS.Infrastructure.Persistence.PostgreSQL;

public class QuerySms : IQuerySms
{
    private readonly NpgsqlDataSource _dataSource;

    public QuerySms(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Sms?> FindByIdAsync(int id, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            select * from messaging.sms
                                            where id = @Id
                                            limit 1;
                                            """,
            new
            {
                Id = id
            },
            cancellationToken: token);

        return await connection.QuerySingleOrDefaultAsync<Sms>(command);
    }

    public async Task<Sms?> FindByExternalIdAsync(string externalId, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            select * from messaging.sms
                                            where external_id = @ExternalId
                                            limit 1;
                                            """,
            new
            {
                ExternalId = externalId
            },
            cancellationToken: token);

        return await connection.QuerySingleOrDefaultAsync<Sms>(command);
    }

    public async Task<List<Sms>> FindsPendingAsync(CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);
        await using var transact = await connection.BeginTransactionAsync(token);

        var command = new CommandDefinition("""
                                            with pending as (
                                                select id from messaging.sms
                                                where status = 'Pending'
                                                   or (status = 'Publishing' and locked_until < now())
                                                order by created_at
                                                limit 100
                                                for update skip locked
                                            )
                                            update messaging.sms
                                            set status = 'Publishing',
                                                locked_until = now() + interval '2 minutes'
                                            from pending
                                            where sms.id = pending.id
                                            returning sms.*;
                                            """,
            transaction: transact,
            cancellationToken: token);

        var smsPending = (await connection.QueryAsync<Sms>(command))
            .ToList();

        await transact.CommitAsync(token);

        return smsPending;
    }

    public async Task<int> LeaseToPublishedAsync(IEnumerable<string> externalIds, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            update messaging.sms
                                            set 
                                                status = 'Published',
                                                locked_until = null
                                            where external_id = any(@ExternalIds)
                                              and status = 'Publishing';
                                            """,
            new
            {
                ExternalIds = externalIds.ToArray()
            },
            cancellationToken: token);

        return await connection.ExecuteAsync(command);
    }

    public async Task<int> LeaseToPendingAsync(IEnumerable<string> externalIds, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            update messaging.sms
                                            set status = 'Pending',
                                                locked_until = null
                                            where external_id = any(@ExternalIds)
                                              and status = 'Publishing';
                                            """,
            new
            {
                ExternalIds = externalIds.ToArray()
            },
            cancellationToken: token);

        return await connection.ExecuteAsync(command);
    }

    public async Task<Sms> InsertAsync(Sms sms, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            insert into messaging.sms (content,
                                                             content_type,
                                                             sender,
                                                             recipient,
                                                             medium,
                                                             status,
                                                             created_at,
                                                             published_at,
                                                             completed_at)
                                            values (@Content,
                                                    @ContentType,
                                                    @Sender,
                                                    @Recipient,
                                                    @Medium,
                                                    @Status,
                                                    @CreatedAt,
                                                    @PublishedAt,
                                                    @CompletedAt)
                                            returning *;
                                            """, sms,
            cancellationToken: token);

        return await connection.QuerySingleAsync<Sms>(command);
    }

    public async Task<Sms> UpdateAsync(Sms sms, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            update messaging.sms
                                            set content = @Content,
                                                content_type = @ContentType,
                                                sender = @Sender,
                                                recipient = @Recipient,
                                                medium = @Medium,
                                                status = @Status,
                                                published_at = @PublishedAt,
                                                completed_at = @CompletedAt
                                            where external_id = @ExternalId
                                            returning *;
                                            """, sms,
            cancellationToken: token);

        return await connection.QuerySingleOrDefaultAsync<Sms>(command) ?? sms;
    }

    public async Task<int> DeleteByIdAsync(int id, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            delete from messaging.sms
                                            where id = @Id;
                                            """,
            new
            {
                Id = id
            },
            cancellationToken: token);

        return await connection.ExecuteAsync(command);
    }

    public async Task<int> DeleteByExternalIdAsync(string externalId, CancellationToken token = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(token);

        var command = new CommandDefinition("""
                                            delete from messaging.sms
                                            where external_id = @ExternalId;
                                            """,
            new
            {
                ExternalId = externalId
            },
            cancellationToken: token);

        return await connection.ExecuteAsync(command);
    }
}