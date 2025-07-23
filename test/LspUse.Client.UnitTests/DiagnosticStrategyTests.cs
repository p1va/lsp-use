using LspUse.Application.Configuration;
using Xunit;

namespace LspUse.Client.UnitTests;

public class DiagnosticStrategyTests
{
    [Fact]
    public void DiagnosticsSettings_PullDefaults_Should_Return_Pull_Strategy()
    {
        // Act
        var settings = DiagnosticsSettings.PullDefaults;

        // Assert
        Assert.Equal(DiagnosticStrategy.Pull, settings.Strategy);
    }

    [Fact]
    public void DiagnosticsSettings_PushDefaults_Should_Return_Push_Strategy_With_Timeout()
    {
        // Act
        var settings = DiagnosticsSettings.PushDefaults;

        // Assert
        Assert.Equal(DiagnosticStrategy.Push, settings.Strategy);
        Assert.Equal(3000, settings.WaitTimeoutMs);
    }

    [Fact]
    public void DiagnosticStrategy_Enum_Should_Have_Expected_Values()
    {
        // Assert
        Assert.Equal(0, (int)DiagnosticStrategy.Pull);
        Assert.Equal(1, (int)DiagnosticStrategy.Push);
    }

    [Fact]
    public void LspProfile_Should_Support_Diagnostic_Settings()
    {
        // Arrange
        var profile = new LspProfile
        {
            Command = "test-lsp --stdio",
            Extensions = new Dictionary<string, string> { { ".test", "test" } },
            Diagnostics = new DiagnosticsSettings
            {
                Strategy = DiagnosticStrategy.Push,
                WaitTimeoutMs = 5000
            }
        };

        // Assert
        Assert.NotNull(profile.Diagnostics);
        Assert.Equal(DiagnosticStrategy.Push, profile.Diagnostics.Strategy);
        Assert.Equal(5000, profile.Diagnostics.WaitTimeoutMs);
    }

    [Theory]
    [InlineData(".cs", true)] // Should match C# extension
    [InlineData(".ts", false)] // Should not match TypeScript extension in this profile
    [InlineData(".unknown", false)] // Unknown extension
    public void LspProfile_With_Csharp_Extensions_Should_Support_Expected_Extensions(string extension, bool expectedSupport)
    {
        // Arrange
        var profile = new LspProfile
        {
            Command = "Microsoft.CodeAnalysis.LanguageServer --stdio",
            Extensions = new Dictionary<string, string>
            {
                { ".cs", "csharp" },
                { ".csx", "csharp" }
            },
            Diagnostics = DiagnosticsSettings.PullDefaults
        };

        // Act
        var supports = profile.SupportsExtension(extension);

        // Assert
        Assert.Equal(expectedSupport, supports);
    }
}
