namespace RAC.Domain;

// Single durable checkpoint for the local synthetic simulation.
// The JSON includes prices, open positions, risk state and event sequence.
public sealed class SimulationCheckpoint
{
    public int Id { get; set; }
    public required string StateJson { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public int Tick { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
