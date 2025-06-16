using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodePoviderExtension.MCP
{
    /// <summary>
    /// Унифицированный MCP клиент для работы с Tools, Resources и Prompts.
    /// </summary>
    public interface IMcpClient : IDisposable
    {
        // Lifecycle
        Task InitializeAsync(CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);
        bool IsConnected { get; }
        
        // Core capabilities
        IMcpToolService Tools { get; }
        IMcpResourceService Resources { get; }
        IMcpPromptService Prompts { get; }
        
        // Server info
        Task<McpServerInfo> GetServerInfoAsync(CancellationToken ct = default);
        Task<McpCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
        
        // Events
        event EventHandler<McpConnectionChangedEventArgs> ConnectionChanged;
        event EventHandler<McpErrorEventArgs> ErrorOccurred;
    }

    /// <summary>
    /// Сервис для работы с MCP Tools (исполнение удаленных задач).
    /// </summary>
    public interface IMcpToolService
    {
        Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken ct = default);
        Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken ct = default);
        
        // Convenience methods
        Task<object> AnalyzeCodeAsync(string code, string language, CancellationToken ct = default);
        Task<object> GenerateCodeAsync(string description, string language, string context, CancellationToken ct = default);
        Task<object> RefactorCodeAsync(string code, string instructions, CancellationToken ct = default);
        Task<object> FindPatternsAsync(string projectPath, string pattern, CancellationToken ct = default);
    }

    /// <summary>
    /// Сервис для работы с MCP Resources (файлы, документы, контекст).
    /// </summary>
    public interface IMcpResourceService
    {
        Task<IEnumerable<McpResource>> ListResourcesAsync(CancellationToken ct = default);
        Task<string> ReadResourceAsync(string uri, CancellationToken ct = default);
        Task<bool> SubscribeToResourceAsync(string uri, CancellationToken ct = default);
        Task<bool> UnsubscribeFromResourceAsync(string uri, CancellationToken ct = default);
        
        // Convenience methods
        Task<IEnumerable<McpResource>> FindResourcesAsync(string pattern, CancellationToken ct = default);
        Task<string> ReadProjectFileAsync(string relativePath, CancellationToken ct = default);
    }

    /// <summary>
    /// Сервис для работы с MCP Prompts (шаблоны для AI).
    /// </summary>
    public interface IMcpPromptService
    {
        Task<IEnumerable<McpPrompt>> ListPromptsAsync(CancellationToken ct = default);
        Task<McpPromptResult> GetPromptAsync(string name, Dictionary<string, object> arguments, CancellationToken ct = default);
        
        // Convenience methods
        Task<McpPromptResult> GetCodeAnalysisPromptAsync(string code, string language, CancellationToken ct = default);
        Task<McpPromptResult> GetCodeGenerationPromptAsync(string description, string language, string context, CancellationToken ct = default);
        Task<McpPromptResult> GetRefactoringPromptAsync(string code, string instructions, CancellationToken ct = default);
    }

    #region Models

    public class McpResource
    {
        public required string Uri { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public string? MimeType { get; init; }
        public Dictionary<string, object> Annotations { get; init; } = new();
    }

    public class McpPrompt
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required List<McpPromptArgument> Arguments { get; init; }
    }

    public class McpPromptArgument
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required bool Required { get; init; }
    }

    public class McpPromptResult
    {
        public required string Description { get; init; }
        public required List<McpPromptMessage> Messages { get; init; }
    }

    public class McpPromptMessage
    {
        public required string Role { get; init; } // "user" | "assistant" | "system"
        public required McpPromptContent Content { get; init; }
    }

    public class McpPromptContent
    {
        public required string Type { get; init; } // "text" | "image"
        public string? Text { get; init; }
        public object? Data { get; init; }
    }

    public class McpServerInfo
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public string? Description { get; init; }
        public string? Author { get; init; }
        public string? Homepage { get; init; }
    }

    public class McpCapabilities
    {
        public McpToolsCapability? Tools { get; init; }
        public McpResourcesCapability? Resources { get; init; }
        public McpPromptsCapability? Prompts { get; init; }
        public McpLoggingCapability? Logging { get; init; }
    }

    public class McpToolsCapability
    {
        public bool ListChanged { get; init; }
    }

    public class McpResourcesCapability
    {
        public bool Subscribe { get; init; }
        public bool ListChanged { get; init; }
    }

    public class McpPromptsCapability
    {
        public bool ListChanged { get; init; }
    }

    public class McpLoggingCapability
    {
        // Logging capabilities
    }

    #endregion

    #region Events

    public class McpConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? Reason { get; }

        public McpConnectionChangedEventArgs(bool isConnected, string? reason = null)
        {
            IsConnected = isConnected;
            Reason = reason;
        }
    }

    public class McpErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Context { get; }

        public McpErrorEventArgs(Exception exception, string context)
        {
            Exception = exception;
            Context = context;
        }
    }

    #endregion
}