using System.Text;
using RabbitMQ.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Velqora.Core;
using Velqora.Infrastructure;
using Xunit;
namespace Velqora.IntegrationTests;

public sealed class PipelineTests
{
    [Fact]
    public async Task Durable_ingestion_detection_case_approval_and_restart()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var services=await IntegrationServices.StartAsync(true,ct);
        var options=new DbContextOptionsBuilder<SocDb>().UseNpgsql(services.Postgres).Options;
        await using var db=new SocDb(options);
        await db.Database.MigrateAsync(ct);
        var rule=new DetectionRule{Name="Login failures"};
        var sequence=new DetectionRule{Name="Success after abuse",Kind=RuleKind.FailureThenSuccess,Severity=Severity.Critical};
        db.Rules.AddRange(rule,sequence);
        var scenario=new ScenarioRun{Scenario="login-abuse",Device=services.Name,Account="lab",TotalEvents=13};
        db.ScenarioRuns.Add(scenario);
        var asset=new Asset{Name=scenario.Device,Account="lab",Owner="Test"};
        var book=new Playbook{Name="Contain",Action="isolate-device"};
        db.Assets.Add(asset);db.Playbooks.Add(book);await db.SaveChangesAsync(ct);
        var inputs=Enumerable.Range(0,13).Select(i=>Scenarios.Event(scenario,i)).ToArray();
        var ingestion=new IngestionService(db);
        Assert.Equal(13,(await ingestion.IngestAsync(inputs,scenario.Id,ct)).Accepted);
        Assert.Equal(13,(await ingestion.IngestAsync(inputs,scenario.Id,ct)).Duplicates);
        Assert.Equal(13,await db.Events.CountAsync(ct));
        Assert.Equal(13,await db.Outbox.CountAsync(ct));
        var trend=await ReportingQueries.DailyActivityAsync(db,DateTimeOffset.UtcNow.AddDays(-1),ct);
        Assert.Equal(13,Assert.Single(trend).Count);
        await Assert.ThrowsAsync<DomainException>(()=>ingestion.IngestAsync([inputs[0] with{Account="different"}],scenario.Id,ct));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"RabbitMq",services.Rabbit}}).Build();
        await using var bus=new MessageBus(config);
        await using var channel=await bus.ChannelAsync(true,ct);
        foreach(var input in inputs)await MessageBus.PublishAsync(channel,input.Id,ct);
        using var redis=await ConnectionMultiplexer.ConnectAsync(services.Redis);
        for(int i=0;i<inputs.Length;i++)
        {
            var message=await channel.BasicGetAsync(MessageBus.Queue,false,ct);
            Assert.NotNull(message);
            var id=Guid.Parse(Encoding.UTF8.GetString(message.Body.Span));
            await using var consumeDb=new SocDb(options);
            await new DetectionService(consumeDb,redis,NullLogger<DetectionService>.Instance).ProcessAsync(id,ct);
            await channel.BasicAckAsync(message.DeliveryTag,false,ct);
        }
        Assert.Equal(2,await db.Alerts.CountAsync(ct));
        await using(var restartedDb=new SocDb(options))
            await new DetectionService(restartedDb,redis,NullLogger<DetectionService>.Instance).ProcessAsync(inputs[0].Id,ct);
        Assert.Equal(2,await db.Alerts.CountAsync(ct));
        Assert.Equal(13,await db.Processed.CountAsync(ct));
                await channel.BasicPublishAsync("velqora.dead","event",true,new BasicProperties{Persistent=true},Encoding.UTF8.GetBytes(inputs[0].Id.ToString()),ct);
        Assert.Equal(1,await bus.ReplayAsync(ct));
        var replayed=await channel.BasicGetAsync(MessageBus.Queue,false,ct);
        Assert.NotNull(replayed);
        await using(var replayDb=new SocDb(options))
            await new DetectionService(replayDb,redis,NullLogger<DetectionService>.Instance).ProcessAsync(inputs[0].Id,ct);
        await channel.BasicAckAsync(replayed.DeliveryTag,false,ct);
        Assert.Equal(2,await db.Alerts.CountAsync(ct));
        await channel.BasicPublishAsync("velqora.events","event",true,new BasicProperties{Persistent=true},Encoding.UTF8.GetBytes("malformed-delivery"),ct);
        var poison=await channel.BasicGetAsync(MessageBus.Queue,false,ct);
        Assert.NotNull(poison);
        await channel.BasicNackAsync(poison.DeliveryTag,false,false,ct);
        Assert.Equal(0,await bus.ReplayAsync(ct));
        Assert.Equal(1u,(await bus.InspectAsync(ct)).DeadLetters);
        await db.Entry(asset).ReloadAsync(ct);
        var alert=await db.Alerts.FirstAsync(ct);
        var incident=new Incident{Title="Integration case",Severity=Severity.High};
        db.Incidents.Add(incident);
        db.IncidentAlerts.Add(new(){IncidentId=incident.Id,AlertId=alert.Id});
        var run=new PlaybookRun{IncidentId=incident.Id,AssetId=asset.Id,PlaybookId=book.Id,RequestedBy="analyst",ActionSnapshot=book.Action,PlaybookVersion=book.Version};
        db.PlaybookRuns.Add(run);await db.SaveChangesAsync(ct);
        await new OperationsService(db).AutomateAsync(ct);
        Assert.False(asset.Isolated);
        book.Action="lock-account";await db.SaveChangesAsync(ct);
        run.Status=ApprovalStatus.Approved;run.ApprovedBy="admin";await db.SaveChangesAsync(ct);
        await new OperationsService(db).AutomateAsync(ct);
        Assert.True(asset.Isolated);Assert.False(asset.AccountLocked);Assert.Equal(ApprovalStatus.Executed,run.Status);
        await new OperationsService(db).AutomateAsync(ct);
        Assert.Single(await db.Timeline.Where(x=>x.IncidentId==incident.Id).ToListAsync(ct));
        await using var first=new SocDb(options);
        await using var second=new SocDb(options);
        var a=await first.Incidents.SingleAsync(x=>x.Id==incident.Id,ct);
        var b=await second.Incidents.SingleAsync(x=>x.Id==incident.Id,ct);
        a.Summary="first edit";b.Summary="conflicting edit";await first.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>second.SaveChangesAsync(ct));
        var audit=new AuditEvent{ActorId="test",Action="immutable",Resource=incident.Id.ToString()};
        db.Audit.Add(audit);await db.SaveChangesAsync(ct);audit.Detail="tamper";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Database_enforces_audit_immutability()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var services=await IntegrationServices.StartAsync(false,ct);
        await using var db=new SocDb(new DbContextOptionsBuilder<SocDb>().UseNpgsql(services.Postgres).Options);
        await db.Database.MigrateAsync(ct);
        var audit=new AuditEvent{ActorId="test",Action="seed",Resource="audit"};
        db.Audit.Add(audit);await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditEvents\"",ct));
    }
}
