using LspUse.LanguageServerClient.Models;

namespace LspUse.LanguageServerClient;

public static class ExtensionMethods
{
    public static ZeroBasedPosition ToZeroBasedPosition(
        this (uint Line, uint Character) editorPosition) =>
        new()
        {
            Line = editorPosition.Line - 1,
            Character = editorPosition.Character - 1
        };

    public static TextDocumentIdentifier ToDocumentIdentifier(this Uri fileUri) =>
        new()
        {
            Uri = fileUri
        };
}
