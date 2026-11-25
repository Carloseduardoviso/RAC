namespace RAC.Domain;

public sealed class RacAgentTaskRecord
{
    public Guid Id { get; set; }
    public string Leader { get; set; } = "";
    public string Specialist { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
