namespace RAC.Application;

public enum RacMessageKind { TaskRequest, Report, RiskNotice }

public sealed record RacAgentMessage(Guid Id, Guid CorrelationId, string Sender, string Recipient,
    RacMessageKind Kind, string Content, DateTimeOffset CreatedUtc);

public static class RacAgentCommunication
{
    public static RacAgentMessage Create(string sender, string recipient, RacMessageKind kind,
        string content, Guid correlationId, DateTimeOffset createdUtc)
    {
        var from = RacOrganization.All.SingleOrDefault(x => x.Id == sender);
        var to = RacOrganization.All.SingleOrDefault(x => x.Id == recipient);
        if (from is null || to is null || sender == recipient)
            throw new ArgumentException("Invalid agent route.");
        if (!Enum.IsDefined(kind) || correlationId == Guid.Empty || string.IsNullOrWhiteSpace(content) || content.Length > 4096)
            throw new ArgumentException("Invalid message.");
        bool allowed = kind switch
        {
            RacMessageKind.TaskRequest => to.ReportsTo == sender,
            RacMessageKind.Report => from.ReportsTo == recipient,
            RacMessageKind.RiskNotice => sender == "carlos" && recipient == "ana",
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Unauthorized communication route.");
        return new(Guid.NewGuid(), correlationId, sender, recipient, kind, content, createdUtc.ToUniversalTime());
    }
}
