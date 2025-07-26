namespace LspUse.LanguageServerClient;

public interface ILanguageServerProcess : IAsyncDisposable
{
    Stream StandardInput { get; }
    Stream StandardOutput { get; }
    void Start();
}
