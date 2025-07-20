# Nuget source where the tool is published to
public_nuget_source := "https://api.nuget.org/v3/index.json"

# LSP download location
roslyn_nuget  := "https://pkgs.dev.azure.com/azure-public/vside/_packaging/vs-impl/nuget/v3/index.json"
roslyn_version := "5.0.0-1.25353.13"

# Local development environment. These can be overwritten when ivoking via just roslyn_platform=win-x64 {command}
roslyn_platform := "linux-x64"
roslyn_path := "./../src/LspUse.McpServer/lsp"

tool_package_id := "LspUse.Csharp"
tool_version := "0.0.1"

default:
  @just --list --list-prefix " -- "

interactive:
  @just --choose

#
# Downloading and running Roslyn (C# LSP)
#

# Lists available platforms
[group('roslyn')]
roslyn-list-platforms:
    @echo "win-x64\nwin-arm64\nlinux-x64\nlinux-arm64\nlinux-musl-x64\nlinux-musl-arm64\nosx-x64\nosx-arm64\nneutral"

[group('roslyn')]
[private]
cleanup-download:
  mv src/LspUse.McpServer/lsp/microsoft.codeanalysis.languageserver.{{roslyn_platform}}/{{roslyn_version}}/content/LanguageServer/{{roslyn_platform}}/* src/LspUse.McpServer/lsp
  rm -drf src/LspUse.McpServer/lsp/microsoft.codeanalysis.languageserver.{{roslyn_platform}}/


# Downloads roslyn locally
[group('roslyn')]
roslyn-download:
  @echo "Downloading Roslyn {{roslyn_platform}} {{roslyn_version}} to {{roslyn_path}}"
  dotnet restore scripts/ServerDownload.csproj \
       --source {{roslyn_nuget}} \
       /p:DownloadPath={{roslyn_path}} \
       /p:PackageName=Microsoft.CodeAnalysis.LanguageServer.{{roslyn_platform}} \
       /p:PackageVersion={{roslyn_version}}
       
  just \
    --set roslyn_platform {{roslyn_platform}} \
    cleanup-download


# Runs roslyn with stdio communication
[group('roslyn')]
roslyn-run-stdio:
  dotnet \
    {{roslyn_path}}/Microsoft.CodeAnalysis.LanguageServer.dll \
      --logLevel Information \
      --extensionLogDirectory logs \
      --stdio

# Runs roslyn with named pipes communication
[group('roslyn')]
roslyn-run-named-pipes pipe-name:
  dotnet \
    {{roslyn_path}}/Microsoft.CodeAnalysis.LanguageServer.dll \
      --logLevel=Information \
      --extensionLogDirectory logs \
      --pipe {{pipe-name}}
      
#
# Local development
#

# Installs `lsp-use` from source
[group('tool')]
install:
  just clean-nupkg
  just pack
  dotnet tool install \
    --global \
    --source ./src/LspUse.McpServer/nupkg \
    {{tool_package_id}}.{{roslyn_platform}}

# Uninstalls `lsp-use`
[group('tool')]
remove:
  dotnet tool uninstall -g {{tool_package_id}}.{{roslyn_platform}} || true

[group('tool')]
reinstall:
  just remove
  just install

# Runs `lsp-use` against this codebase
[group('tool')]
run:
  lsp-use --sln "lsp-use.sln" --workspace . 

[group('release')]
list-nupkg:
    ls -l src/LspUse.McpServer/nupkg/

[group('release')]
clean-nupkg:
  @echo "Removing local *.nupkg"
  just list-nupkg
  rm -drf src/LspUse.McpServer/nupkg/*

[group('release')]
pack:
  dotnet pack src/LspUse.McpServer/LspUse.McpServer.csproj \
    -c Release \
    --runtime {{roslyn_platform}} \
    -p:RuntimeIdentifier={{roslyn_platform}} \
    -p:PackageBaseId={{tool_package_id}} \
    -p:PackageVersion={{tool_version}} \

[group('release')]
publish:
  @echo "You are going to publish the following nuget packages"
  ls -l src/LspUse.McpServer/nupkg/*.nupkg
  just publish-w-confirm

[private]
[group('release')]
[confirm("Did you review the packages about to be published?")]
publish-w-confirm:
  just publish-no-confirm

[private]
[group('release')]
publish-no-confirm:
  dotnet nuget push src/LspUse.McpServer/nupkg/*.nupkg \
    --api-key $NUGET_API_KEY \
    --source {{public_nuget_source}}

[group('release')]
tag version message:
  git tag -a {{version}} -m "{{message}}"
  git push --follow-tags
  gh repo view --web

#
# Prepare env for coding agents
#

[private]
pull-repo repo dir:
  git clone --depth 1 https://github.com/{{repo}}.git .external/{{dir}}

[group('coding-agents-env')]
pull-dotnet-docs:
  just pull-repo dotnet/docs dotnet-docs

[group('coding-agents-env')]
pull-mcp-sdk:
  just pull-repo modelcontextprotocol/csharp-sdk mcp-csharp-sdk

[group('coding-agents-env')]
pull-roslyn:
  just pull-repo dotnet/roslyn roslyn

[group('scripts')]
check-inotify:
    ./scripts/inotify-consumers.sh



