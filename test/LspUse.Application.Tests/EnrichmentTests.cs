using LspUse.Application;
using LspUse.Application.Models;
using Xunit;

namespace LspUse.Application.Tests;

public class EnrichmentTests
{
    [Fact]
    public async Task EnrichWithTextAsync_SingleLine_ExtractsCorrectText()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        var testContent = "line1\nline2\nsome test content here\nline4";
        await File.WriteAllTextAsync(testFile, testContent);

        var locations = new[]
        {
            new SymbolLocation
            {
                FilePath = new Uri(testFile),
                StartLine = 3,
                StartCharacter = 6,
                EndLine = 3,
                EndCharacter = 10
            }
        };

        try
        {
            // Act
            var enrichedLocations = await locations.EnrichWithTextAsync();
            var result = enrichedLocations.First();

            // Assert
            Assert.Equal("some test content here", result.Text);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public async Task EnrichWithTextAsync_MultiLine_ExtractsCorrectText()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        var testContent = "line1\nline2\npublic class TestClass\n{\n    public void Method()\n    {\n    }\n}";
        await File.WriteAllTextAsync(testFile, testContent);

        var locations = new[]
        {
            new SymbolLocation
            {
                FilePath = new Uri(testFile),
                StartLine = 3,
                StartCharacter = 1,
                EndLine = 4,
                EndCharacter = 2
            }
        };

        try
        {
            // Act
            var enrichedLocations = await locations.EnrichWithTextAsync();
            var result = enrichedLocations.First();

            // Assert
            Assert.Equal("public class TestClass {", result.Text);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public async Task EnrichWithTextAsync_NonExistentFile_ReturnsNullText()
    {
        // Arrange
        var nonExistentFile = "/tmp/nonexistent.cs";
        var locations = new[]
        {
            new SymbolLocation
            {
                FilePath = new Uri(nonExistentFile),
                StartLine = 1,
                StartCharacter = 1,
                EndLine = 1,
                EndCharacter = 5
            }
        };

        // Act
        var enrichedLocations = await locations.EnrichWithTextAsync();
        var result = enrichedLocations.First();

        // Assert
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task EnrichWithTextAsync_GroupsByFile_ReadsEachFileOnce()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        var testContent = "line1\nline2\nsome test content here\nline4";
        await File.WriteAllTextAsync(testFile, testContent);

        var locations = new[]
        {
            new SymbolLocation
            {
                FilePath = new Uri(testFile),
                StartLine = 1,
                StartCharacter = 1,
                EndLine = 1,
                EndCharacter = 5
            },
            new SymbolLocation
            {
                FilePath = new Uri(testFile),
                StartLine = 2,
                StartCharacter = 1,
                EndLine = 2,
                EndCharacter = 5
            }
        };

        try
        {
            // Act
            var enrichedLocations = await locations.EnrichWithTextAsync();
            var results = enrichedLocations.ToList();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("line1", results[0].Text);
            Assert.Equal("line2", results[1].Text);
        }
        finally
        {
            File.Delete(testFile);
        }
    }
}