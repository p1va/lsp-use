using System.Text.Json;
using LspUse.LanguageServerClient.Models;
using Xunit;

namespace LspUse.Client.UnitTests;

public class DiagnosticCodeConverterTests
{
    [Fact]
    public void Should_Deserialize_String_Code()
    {
        // Arrange
        var json = """
        {
            "range": {
                "start": { "line": 0, "character": 0 },
                "end": { "line": 0, "character": 5 }
            },
            "message": "CS1234: Some C# error",
            "severity": 1,
            "code": "CS1234",
            "source": "csharp"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<Diagnostic>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CS1234", result.Code);
    }

    [Fact]
    public void Should_Deserialize_Integer_Code()
    {
        // Arrange
        var json = """
        {
            "range": {
                "start": { "line": 434, "character": 8 },
                "end": { "line": 434, "character": 14 }
            },
            "message": "'unused' is declared but its value is never read.",
            "severity": 4,
            "code": 6133,
            "source": "typescript"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<Diagnostic>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("6133", result.Code);
    }

    [Fact]
    public void Should_Handle_Null_Code()
    {
        // Arrange
        var json = """
        {
            "range": {
                "start": { "line": 0, "character": 0 },
                "end": { "line": 0, "character": 5 }
            },
            "message": "Some error without code",
            "severity": 1,
            "code": null,
            "source": "test"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<Diagnostic>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Code);
    }

    [Fact]
    public void Should_Serialize_String_Code()
    {
        // Arrange
        var diagnostic = new Diagnostic
        {
            Message = "Test error",
            Code = "CS1234",
            Severity = DiagnosticSeverity.Error
        };

        // Act
        var json = JsonSerializer.Serialize(diagnostic);
        var result = JsonSerializer.Deserialize<Diagnostic>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CS1234", result.Code);
    }
}
