using Dapper;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence.PostgreSQL;
using SMS.Infrastructure.Test.Persistence.Context;

namespace SMS.Infrastructure.Test.Persistence.PostgreSQL;

public class QuerySmsTest : IClassFixture<DbFixture>
{
    private readonly DbFixture _dbFixture;
    private readonly QuerySms _querySms;

    public QuerySmsTest(DbFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _querySms = new QuerySms(dbFixture.NpgsqlSource);

        // FindsPendingAsync claims every pending row, so each test starts from an empty table
        using var connection = dbFixture.NpgsqlSource.OpenConnection();

        connection.Execute("truncate table messaging.sms restart identity;");
    }

    private static Sms Entity(string status = "Pending", DateTime? createdAt = null)
    {
        return new Sms
        {
            Id = default,
            ExternalId = default!,
            Content = "Sample Content",
            ContentType = "text/plain",
            Sender = "Sample Sender",
            Recipient = "+639170000000",
            Medium = "sms",
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    // ---------- InsertAsync ----------
    [Fact]
    public async Task InsertAsync_Sms_ExternalIdGenerated()
    {
        // arrange
        var sms = Entity();

        // act
        var inserted = await _querySms.InsertAsync(sms, TestContext.Current.CancellationToken);

        // assert
        Assert.True(inserted.Id > 0);
        Assert.True(Guid.TryParse(inserted.ExternalId, out _));
        Assert.Equal(sms.Content, inserted.Content);
        Assert.Equal(sms.ContentType, inserted.ContentType);
        Assert.Equal(sms.Sender, inserted.Sender);
        Assert.Equal(sms.Recipient, inserted.Recipient);
        Assert.Equal(sms.Medium, inserted.Medium);
        Assert.Equal("Pending", inserted.Status);
    }

    // ---------- FindByExternalIdAsync ----------
    [Fact]
    public async Task FindByExternalIdAsync_Found_Sms()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var found = await _querySms.FindByExternalIdAsync(insert.ExternalId, TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(found);
        Assert.Equal(insert.Id, found.Id);
        Assert.Equal(insert.ExternalId, found.ExternalId);
        Assert.Equal(insert.Content, found.Content);
    }

    [Fact]
    public async Task FindByExternalIdAsync_NotFound_Null()
    {
        // act
        var found = await _querySms.FindByExternalIdAsync(Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        // assert
        Assert.Null(found);
    }

    // ---------- FindByIdAsync ----------
    [Fact]
    public async Task FindByIdAsync_Found_Sms()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var found = await _querySms.FindByIdAsync(insert.Id, TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(found);
        Assert.Equal(insert.Id, found.Id);
        Assert.Equal(insert.ExternalId, found.ExternalId);
    }

    [Fact]
    public async Task FindByIdAsync_NotFound_Null()
    {
        // act
        var found = await _querySms.FindByIdAsync(int.MaxValue, TestContext.Current.CancellationToken);

        // assert
        Assert.Null(found);
    }

    // ---------- FindsPendingAsync ----------
    [Fact]
    public async Task FindsPendingAsync_Pending_ClaimedAsPublishingWithLease()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var pending = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);

        // assert
        var pendingSingle = Assert.Single(pending);

        Assert.Equal(insert.ExternalId, pendingSingle.ExternalId);
        Assert.Equal("Publishing", pendingSingle.Status);
        Assert.NotNull(pendingSingle.LockedUntil);
        Assert.True(pendingSingle.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task FindsPendingAsync_ActiveLease_Skipped()
    {
        // arrange
        var entity = Entity();

        await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var initClaim = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);
        var nextClaim = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);

        // assert
        Assert.Single(initClaim);
        Assert.Empty(nextClaim);
    }

    [Fact]
    public async Task FindsPendingAsync_ExpiredLease_ClaimedAgain()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // simulate that a 'Publishing' sms has an expired locked
        {
            await using var connection =
                await _dbFixture.NpgsqlSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

            await connection.ExecuteAsync("""
                                          update messaging.sms
                                          set status = 'Publishing',
                                              locked_until = now() + @Offset
                                          where external_id = @ExternalId;
                                          """,
                new
                {
                    ExternalId = insert.ExternalId,
                    Offset = TimeSpan.FromMinutes(-1)
                });
        }

        // act
        var claimed = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);
        
        // assert
        var claimSingle = Assert.Single(claimed);

        Assert.Equal(insert.ExternalId, claimSingle.ExternalId);
        Assert.True(claimSingle.LockedUntil > DateTime.UtcNow);
    }

    [Theory]
    [InlineData("Published")]
    [InlineData("Delivered")]
    [InlineData("Failed")]
    public async Task FindsPendingAsync_NotPending_Ignored(string status)
    {
        // arrange
        var entity = Entity(status);
        
        await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var claimed = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);

        // assert
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task FindsPendingAsync_MoreThanLimit_HundredClaimed()
    {
        // arrange
        var created = DateTime.UtcNow.AddHours(-1);
        var inserts = new List<Sms>();

        for (var i = 0; i < 101; i++)
        {
            var entity = Entity(createdAt: created.AddSeconds(i));
            var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);
            
            inserts.Add(insert);
        }

        // act
        var claimed = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(100, claimed.Count);
        Assert.DoesNotContain(claimed, x => x.ExternalId == inserts[^1].ExternalId);

        // assert if newest more than limit status is still pending
        var newest = await _querySms.FindByExternalIdAsync(inserts[^1].ExternalId, TestContext.Current.CancellationToken);

        Assert.NotNull(newest);
        Assert.Equal("Pending", newest.Status);
    }

    // ---------- LeaseToPublishedAsync ----------
    [Fact]
    public async Task LeaseToPublishedAsync_Publishing_PublishedAndLockCleared()
    {
        // arrange
        var entity = Entity();
        
        await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var claimed = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);
        var affected =
            await _querySms.LeaseToPublishedAsync(claimed.Select(x => x.ExternalId), TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(1, affected);

        // assert sms status after lease to publish
        var publish = await _querySms.FindByExternalIdAsync(claimed[0].ExternalId, TestContext.Current.CancellationToken);
        
        Assert.NotNull(publish);
        Assert.Equal("Published", publish.Status);
        Assert.Null(publish.LockedUntil);
        Assert.Null(publish.CompletedAt);
    }

    // ---------- LeaseToPendingAsync ----------
    [Fact]
    public async Task LeaseToPendingAsync_Publishing_PendingAndLockCleared()
    {
        // arrange
        var entity = Entity();
        
        await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);


        // act
        var claimed = await _querySms.FindsPendingAsync(TestContext.Current.CancellationToken);
        var affected =
            await _querySms.LeaseToPendingAsync(claimed.Select(x => x.ExternalId), TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(1, affected);

        // assert sms status after lease to pending
        var pending = await _querySms.FindByExternalIdAsync(claimed[0].ExternalId, TestContext.Current.CancellationToken);
        
        Assert.NotNull(pending);
        Assert.Equal("Pending", pending.Status);
        Assert.Null(pending.LockedUntil);
        Assert.Null(pending.CompletedAt);
    }

    // ---------- UpdateAsync ----------
    [Fact]
    public async Task UpdateAsync_Existing_Updated()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);
        var completedAt = DateTime.UtcNow;

        insert.Status = "Delivered";
        insert.CompletedAt = completedAt;

        // act
        var updated = await _querySms.UpdateAsync(insert, TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(insert.Id, updated.Id);
        Assert.Equal("Delivered", updated.Status);
        
        // check the record in the database
        var record = await _querySms.FindByExternalIdAsync(insert.ExternalId, TestContext.Current.CancellationToken);

        Assert.NotNull(record);
        Assert.Equal(insert.Id, record.Id);
        Assert.Equal("Delivered", record.Status);
        Assert.NotNull(record.CompletedAt);
    }

    [Fact]
    public async Task UpdateAsync_NotFound()
    {
        // arrange
        var entity = Entity();

        entity.ExternalId = Guid.NewGuid().ToString();

        // act
        var updated = await _querySms.UpdateAsync(entity, TestContext.Current.CancellationToken);

        // assert
        Assert.Same(entity, updated);
        
        var record = await _querySms.FindByExternalIdAsync(entity.ExternalId, TestContext.Current.CancellationToken);
        
        Assert.Null(record);
    }

    // ---------- DeleteByIdAsync ----------
    [Fact]
    public async Task DeleteByIdAsync_Existing()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var affected = await _querySms.DeleteByIdAsync(insert.Id, TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(1, affected);
        
        // check the record in the database
        var record = await _querySms.FindByIdAsync(insert.Id, TestContext.Current.CancellationToken);
        
        Assert.Null(record);
    }

    [Fact]
    public async Task DeleteByIdAsync_NotFound_Zero()
    {
        // act
        var affected = await _querySms.DeleteByIdAsync(int.MaxValue, TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(0, affected);
    }

    // ---------- DeleteByExternalIdAsync ----------
    [Fact]
    public async Task DeleteByExternalIdAsync_Existing_One()
    {
        // arrange
        var entity = Entity();
        var insert = await _querySms.InsertAsync(entity, TestContext.Current.CancellationToken);

        // act
        var affected =
            await _querySms.DeleteByExternalIdAsync(insert.ExternalId, TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(1, affected);

        // check the record in the database
        var record = await _querySms.FindByExternalIdAsync(insert.ExternalId, TestContext.Current.CancellationToken);
        
        Assert.Null(record);
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_NotFound_Zero()
    {
        // act
        var affected = await _querySms.DeleteByExternalIdAsync(Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(0, affected);
    }
}