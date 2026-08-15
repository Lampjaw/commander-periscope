using Xunit;

namespace CommanderPeriscope.Lobby.Tests;

public class LobbyCodeGeneratorTests
{
    [Fact]
    public void Generate_ProducesFiveCharacterCode()
    {
        Assert.Equal(5, LobbyCodeGenerator.Generate().Length);
    }

    [Fact]
    public void Generate_AvoidsVisuallyAmbiguousCharacters()
    {
        var code = LobbyCodeGenerator.Generate();
        Assert.DoesNotContain(code, c => c is '0' or 'O' or '1' or 'I');
    }

    [Fact]
    public void Generate_ProducesVariedCodes()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => LobbyCodeGenerator.Generate()).Distinct().ToList();
        Assert.True(codes.Count > 40, "Expected mostly-unique codes across 50 generations.");
    }
}
