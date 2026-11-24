using RAC.Application;
namespace RAC.Tests;
public class PaperStopRecoveryTests
{
    private static readonly DateTimeOffset Now=new(2026,11,24,15,0,0,TimeSpan.Zero);
    private static readonly PaperStopRecovery.Position Position=new(1,"DOTUSDT",5,4,0.02m,3.9m);
    [Fact] public void CalculatesCostsAndLoss()
    {
        var p=PaperStopRecovery.Calculate(false,true,Position,80,0.6m,6,Now,Now);
        Assert.Equal(3.5982m,p.SellPrice);
        Assert.Equal(.017991m,p.Fee);
        Assert.Equal(-2.046991m,p.Realized);
        Assert.Equal(97.973009m,p.EquityAfter);
    }
    [Fact] public void StaleQuoteRejected()=>Assert.Throws<InvalidOperationException>(()=>PaperStopRecovery.Calculate(false,true,Position,80,.6m,6,Now.AddSeconds(-31),Now));
    [Fact] public void FutureQuoteRejected()=>Assert.Throws<InvalidOperationException>(()=>PaperStopRecovery.Calculate(false,true,Position,80,.6m,6,Now.AddSeconds(1),Now));
    [Fact] public void ActiveAccountRejected()=>Assert.Throws<InvalidOperationException>(()=>PaperStopRecovery.Calculate(true,true,Position,80,.6m,6,Now,Now));
    [Fact] public void UnlockedAccountRejected()=>Assert.Throws<InvalidOperationException>(()=>PaperStopRecovery.Calculate(false,false,Position,80,.6m,6,Now,Now));
    [Fact] public void StopNotBreachedRejected()=>Assert.Throws<InvalidOperationException>(()=>PaperStopRecovery.Calculate(false,true,Position,80,.7m,6,Now,Now));
}
