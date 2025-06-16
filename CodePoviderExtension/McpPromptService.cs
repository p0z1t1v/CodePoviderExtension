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
    /// Сервис для работы с MCP промптами (шаблоны для AI задач).
    /// </summary>
    public class McpPromptService : IMcpPromptService, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<McpPromptService> logger;
        private readonly McpCacheService cacheService;

        public McpPromptService(
            HttpClient httpClient,
            ILogger<McpPromptService> logger,
            McpCacheService cacheService)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<IEnumerable<McpPrompt>> ListPromptsAsync(CancellationToken ct = default)
        {
            try
            {
                var cacheKey = "prompts_list";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    logger.LogDebug("Получение списка MCP промптов");

                    var response = await httpClient.GetAsync("mcp/prompts", ct);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var promptsResponse = JsonSerializer.Deserialize<McpPromptsResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var prompts = promptsResponse?.Prompts ?? new List<McpPrompt>();
                    
                    logger.LogInformation("Получено {Count} MCP промптов", prompts.Count);
                    return prompts;

                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения списка промптов");
                return new List<McpPrompt>();
            }
        }

        public async Task<McpPromptResult> GetPromptAsync(string name, Dictionary<string, object> arguments, CancellationToken ct = default)
        {
            try
            {
                var cacheKey = $"prompt_{name}_{GetArgumentsHash(arguments)}";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    logger.LogDebug("Получение промпта: {Name} с аргументами: {Args}", name, string.Join(", ", arguments.Keys));

                    var request = new McpPromptGetRequest 
                    { 
                        Name = name, 
                        Arguments = arguments 
                    };

                    var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("mcp/prompts/get", content, ct);
                    response.EnsureSuccessStatusCode();

                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    var promptResult = JsonSerializer.Deserialize<McpPromptResult>(responseJson, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (promptResult == null)
                    {
                        throw new InvalidOperationException($"Не удалось получить промпт '{name}'");
                    }

                    logger.LogDebug("Получен промпт {Name}, сообщений: {Count}", name, promptResult.Messages.Count);
                    return promptResult;

                }, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения промпта {Name}", name);
                
                // Возвращаем базовый промпт в случае ошибки
                return new McpPromptResult
                {
                    Description = $"Ошибка получения промпта '{name}': {ex.Message}",
                    Messages = new List<McpPromptMessage>
                    {
                        new McpPromptMessage
                        {
                            Role = "user",
                            Content = new McpPromptContent
                            {
                                Type = "text",
                                Text = $"Произошла ошибка при получении промпта '{name}'. Пожалуйста, выполните задачу самостоятельно."
                            }
                        }
                    }
                };
            }
        }

        public async Task<McpPromptResult> GetCodeAnalysisPromptAsync(string code, string language, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["code"] = code,
                ["language"] = language,
                ["task"] = "analyze"
            };

            return await GetPromptAsync("code-analysis", arguments, ct);
        }

        public async Task<McpPromptResult> GetCodeGenerationPromptAsync(string description, string language, string context, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["description"] = description,
                ["language"] = language,
                ["context"] = context,
                ["task"] = "generate"
            };

            return await GetPromptAsync("code-generation", arguments, ct);
        }

        public async Task<McpPromptResult> GetRefactoringPromptAsync(string code, string instructions, CancellationToken ct = default)
        {
            var arguments = new Dictionary<string, object>
            {
                ["code"] = code,
                ["instructions"] = instructions,
                ["task"] = "refactor"
            };

            return await GetPromptAsync("code-refactoring", arguments, ct);
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
            logger.LogDebug("McpPromptService освобожден");
        }
    }

    #region DTOs

    public class McpPromptsResponse
    {
        public List<McpPrompt> Prompts { get; set; } = new();
    }

    public class McpPromptGetRequest
    {
        public required string Name { get; set; }
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    #endregion
}