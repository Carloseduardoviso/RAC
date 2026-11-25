namespace RAC.Domain;
public sealed class RacAgentMessageRecord
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string Sender { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}
