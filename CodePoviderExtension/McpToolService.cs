using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodePoviderExtension.MCP
{
    /// <summary>
    /// Сервис для работы с MCP инструментами (выполнение задач на удаленном сервере).
    /// </summary>
    public class McpToolService : IMcpToolService, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<McpToolService> logger;
        private readonly McpCacheService cacheService;

        public McpToolService(
            HttpClient httpClient,
            ILogger<McpToolService> logger,
            McpCacheService cacheService)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken ct = default)
        {
            try
            {
                var cacheKey = "tools_list";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    logger.LogDebug("Получение списка MCP инструментов");

                    var response = await httpClient.GetAsync("mcp/tools", ct);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var toolsResponse = JsonSerializer.Deserialize<McpToolsResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var tools = toolsResponse?.Tools ?? new List<McpTool>();
                    
                    logger.LogInformation("Получено {Count} MCP инструментов", tools.Count);
                    return tools;

                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения списка инструментов");
                return new List<McpTool>();
            }
        }

        public async Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken ct = default)
        {
            try
            {
                var cacheKey = $"tool_{toolName}_{GetArgumentsHash(arguments)}";
                
                // Некоторые инструменты не стоит кэшировать (например, генерация кода)
                var shouldCache = ShouldCacheToolResult(toolName);
                
                if (shouldCache)
                {
                    return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                        await ExecuteToolAsync(toolName, arguments, ct), TimeSpan.FromMinutes(5));
                }
                else
                {
                    return await ExecuteToolAsync(toolName, arguments, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка вызова инструмента {ToolName}", toolName);
                return new { error = ex.Message };
            }
        }

        private async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken ct)
        {
            logger.LogDebug("Выполнение инструмента: {ToolName} с аргументами: {Args}", 
                toolName, string.Join(", ", arguments.Keys));

            var request = new McpToolCallRequest
            {
                Name = toolName,
                Arguments = arguments
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("mcp/tools/call", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var toolResponse = JsonSerializer.Deserialize<McpToolCallResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (toolResponse?.Content == null)
            {
                throw new InvalidOperationException($"Не удалось выполнить инструмент '{toolName}'");
            }

            logger.LogDebug("Инструмент {ToolName} выполнен успешно", toolName);
            return toolResponse.Content;
        }

        public async Task<object> AnalyzeCodeAsync(string code, string language, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["code"] = code,
                ["language"] = language
            };

            return await CallToolAsync("analyze-code", arguments, ct);
        }

        public async Task<object> GenerateCodeAsync(string description, string language, string context, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["description"] = description,
                ["language"] = language,
                ["context"] = context
            };

            return await CallToolAsync("generate-code", arguments, ct);
        }

        public async Task<object> RefactorCodeAsync(string code, string instructions, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["code"] = code,
                ["instructions"] = instructions
            };

            return await CallToolAsync("refactor-code", arguments, ct);
        }

        public async Task<object> FindPatternsAsync(string projectPath, string pattern, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["projectPath"] = projectPath,
                ["pattern"] = pattern
            };

            return await CallToolAsync("find-patterns", arguments, ct);
        }

        private bool ShouldCacheToolResult(string toolName)
        {
            // Инструменты анализа можно кэшировать, генерации - нет
            return toolName switch
            {
                "analyze-code" => true,
                "find-patterns" => true,
                "get-context" => true,
                "generate-code" => false,
                "refactor-code" => false,
                _ => false
            };
        }

        private string GetArgumentsHash(Dictionary<string, object> arguments)
        {
            // Простое хеширование аргументов для кэширования
            var sortedArgs = arguments.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}");
            var combined = string.Join("|", sortedArgs);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined)).Substring(0, 8);
        }

        public void Dispose()
        {
            logger.LogDebug("McpToolService освобожден");
        }
    }

    #region DTOs

    public class McpToolsResponse
    {
        public List<McpTool> Tools { get; set; } = new();
    }

    public class McpToolCallRequest
    {
        public required string Name { get; set; }
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    public class McpToolCallResponse
    {
        public object? Content { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class McpTool
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object> InputSchema { get; set; } = new();
    }

    #endregion
}