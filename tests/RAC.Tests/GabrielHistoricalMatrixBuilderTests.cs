using RAC.Application;
namespace RAC.Tests;
public sealed class GabrielHistoricalMatrixBuilderTests
{
    [Fact]
    public async Task RejectsMissingArchiveWithoutReturningPartialMatrix()
    {
        var calls=0;
        await Assert.ThrowsAsync<InvalidDataException>(()=>GabrielHistoricalMatrixBuilder.BuildAsync(new[]{"ABCUSDT"},
            (symbol,year,month,ct)=>{
                calls++;
                Assert.Equal("ABCUSDT",symbol);
                Assert.Equal(2026,year);
                Assert.Equal(5,month);
                return Task.FromResult(new GabrielHistoricalMatrixBuilder.Archive([],new string('0',64)));
            },default));
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task RejectsInvalidUniverseBeforeReadingArchives()
    {
        await Assert.ThrowsAsync<ArgumentException>(()=>GabrielHistoricalMatrixBuilder.BuildAsync(new[]{"../ABCUSDT"},
            (_,_,_,_)=>throw new InvalidOperationException("Source must not be called."),default));
    }
}
