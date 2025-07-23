using System.Collections.Generic;
using LspUse.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LspUse.Client.UnitTests;

public class LanguageIdMapperTests
{
    private readonly ILogger<LanguageIdMapper> _logger;

    public LanguageIdMapperTests()
    {
        _logger = NullLogger<LanguageIdMapper>.Instance;
    }

    [Fact]
    public void MapFileToLanguageId_WithCsExtension_ReturnsCsharp()
    {
        // Arrange
        var builtInProfiles = new Dictionary<string, LspProfile>
        {
            ["csharp"] = new()
            {
                Command = "test",
                Extensions = new Dictionary<string, string> { { ".cs", "csharp" } }
            }
        };
        var customProfiles = new Dictionary<string, LspProfile>();
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("Program.cs");

        // Assert
        Assert.Equal("csharp", result);
    }

    [Fact]
    public void MapFileToLanguageId_WithPyExtension_UsesFallbackMapping()
    {
        // Arrange - No profile configured for Python
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>();
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("script.py");

        // Assert
        Assert.Equal("python", result); // Should use default mapping
    }

    [Fact]
    public void MapFileToLanguageId_WithCustomExtension_UsesCustomProfileLanguageId()
    {
        // Arrange - Custom profile with explicit LanguageId
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>
        {
            ["my-typescript"] = new()
            {
                Command = "custom-ts-server",
                Extensions = new Dictionary<string, string>
                {
                    { ".ts", "typescript" },
                    { ".custom", "typescript" }
                }
            }
        };
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("test.custom");

        // Assert
        Assert.Equal("typescript", result);
    }

    [Fact]
    public void MapFileToLanguageId_WithCustomExtensionNoLanguageId_UsesProfileName()
    {
        // Arrange - Custom profile without explicit LanguageId
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>
        {
            ["my-custom-lang"] = new()
            {
                Command = "custom-server",
                Extensions = new Dictionary<string, string> { { ".custom", "my-custom-lang" } }
            }
        };
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("test.custom");

        // Assert
        Assert.Equal("my-custom-lang", result); // Uses profile name as language ID
    }

    [Fact]
    public void MapFileToLanguageId_WithUnknownExtension_ReturnsPlaintext()
    {
        // Arrange
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>();
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("unknown.xyz");

        // Assert
        Assert.Equal("plaintext", result);
    }

    [Fact]
    public void MapFileToLanguageId_WithNoExtension_ReturnsPlaintext()
    {
        // Arrange
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>();
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var result = mapper.MapFileToLanguageId("README");

        // Assert
        Assert.Equal("plaintext", result);
    }

    [Fact]
    public void GetAllMappings_CombinesDefaultAndCustomMappings()
    {
        // Arrange
        var builtInProfiles = new Dictionary<string, LspProfile>();
        var customProfiles = new Dictionary<string, LspProfile>
        {
            ["typescript"] = new()
            {
                Command = "ts-server",
                Extensions = new Dictionary<string, string> { { ".ts", "typescript" } }
            }
        };
        var resolver = new LspProfileResolver(builtInProfiles, customProfiles);
        var mapper = new LanguageIdMapper(resolver, _logger);

        // Act
        var mappings = mapper.GetAllMappings();

        // Assert
        Assert.True(mappings.ContainsKey(".cs")); // From default mappings
        Assert.True(mappings.ContainsKey(".ts")); // From custom profile
        Assert.Equal("typescript", mappings[".ts"]);
        Assert.Equal("csharp", mappings[".cs"]);
    }
}
