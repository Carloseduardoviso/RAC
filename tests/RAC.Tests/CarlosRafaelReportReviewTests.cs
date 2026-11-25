using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosRafaelReportReviewTests
{
    [Fact]
    public void ReadsActualProducerAndRejectsTamperedValues()
    {
        var now=DateTimeOffset.UtcNow;
        var data=new RafaelStoredCapital(new(80,20,90,false),now,now,true,1,2,5,100);
        var message=RafaelFinancialReport.Create(Guid.NewGuid(),now,data,new(30,40,15),TimeSpan.FromMinutes(2),0.01m);
        var evidence=new CarlosRafaelEvidence(message.CorrelationId,message.Id,now,message.Content);
        var result=CarlosRafaelReportReview.Review(evidence);
        Assert.Equal("HUMAN_REVIEW_REQUIRED",result.Status);
        Assert.Equal(100m,result.Capital!.Equity);
        Assert.Equal(message.Id,result.MessageId);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_REPORT",CarlosRafaelReportReview.Review(evidence with {Content=message.Content.Replace("\"Equity\":100","\"Equity\":999")}).Status);
        Assert.Equal("INVALID_REPORT",CarlosRafaelReportReview.Review(evidence with {Content=message.Content.Replace("\"TradingAuthorized\":false","\"TradingAuthorized\":true")}).Status);
        Assert.Equal("INVALID_REPORT",CarlosRafaelReportReview.Review(evidence with {Content="{}"}).Status);
    }
}
