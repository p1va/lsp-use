using LspUse.LanguageServerClient.Handlers;

namespace LspUse.Application.Models;

public record WindowLogRequest;

public record WindowLog(IEnumerable<WindowLogMessage> LogMessages);

public record WindowLogMessage(string Message, LogMessageType MessageType);
