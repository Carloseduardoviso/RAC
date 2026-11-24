using RAC.Application;
namespace RAC.Tests;
public class PaperRecoveryApprovalTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("unauthorized")]
    [InlineData("reviewer")]
    [InlineData("position")]
    [InlineData("symbol")]
    [InlineData("version")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("empty-id")]
    public void ValidatesBoundEvidence(string scenario)
    {
        var now=DateTimeOffset.UtcNow;
        var evidence=new PaperRecoveryApproval.Evidence("reviewer",1,"DOTUSDT","version",now,Guid.NewGuid());
        evidence=scenario switch {
            "reviewer"=>evidence with {Reviewer="other"},
            "position"=>evidence with {PositionId=2},
            "symbol"=>evidence with {Symbol="BTCUSDT"},
            "version"=>evidence with {AccountVersion="old"},
            "expired"=>evidence with {IssuedUtc=now.AddMinutes(-3)},
            "future"=>evidence with {IssuedUtc=now.AddSeconds(1)},
            "empty-id"=>evidence with {ApprovalId=Guid.Empty},
            _=>evidence
        };
        void Validate()=>PaperRecoveryApproval.Validate(evidence,"reviewer",scenario!="unauthorized",1,"DOTUSDT","version",now);
        if(scenario=="valid")Validate();
        else Assert.Throws<InvalidOperationException>(Validate);
    }
}
