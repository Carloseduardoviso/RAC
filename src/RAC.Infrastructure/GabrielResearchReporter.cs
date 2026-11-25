using RAC.Application;
namespace RAC.Infrastructure;

public sealed class GabrielResearchReporter(RacAgentMessageStore messages)
{
    public RacAgentMessage Create(Guid taskId,DateTimeOffset observedUtc,string sourceSha256,string matrixJson)
    {
        if(!string.Equals(sourceSha256,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(matrixJson))),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Matrix hash mismatch.");
        var rows=GabrielExperimentMatrix.Read(matrixJson);
        return GabrielResearchMessage.Create(taskId,observedUtc,sourceSha256,rows);
    }
    public Task<bool> PublishAsync(Guid taskId,DateTimeOffset observedUtc,string sourceSha256,string matrixJson,CancellationToken ct) => messages.SaveAsync(Create(taskId,observedUtc,sourceSha256,matrixJson),ct);
}
