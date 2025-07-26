namespace LspUse.LanguageServerClient;

public interface ILanguageServerManager : IAsyncDisposable
{
    ILspClient Client { get; }
    void Start();
}
