using System;
using System.Collections.Generic;
using System.IO;
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
    /// Сервис для работы с MCP ресурсами (файлы, документы, контекст проекта).
    /// </summary>
    public class McpResourceService : IMcpResourceService, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<McpResourceService> logger;
        private readonly McpCacheService cacheService;
        private readonly HashSet<string> subscribedResources;

        public McpResourceService(
            HttpClient httpClient,
            ILogger<McpResourceService> logger,
            McpCacheService cacheService)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            this.subscribedResources = new HashSet<string>();
        }

        public async Task<IEnumerable<McpResource>> ListResourcesAsync(CancellationToken ct = default)
        {
            try
            {
                var cacheKey = "resources_list";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    logger.LogDebug("Получение списка MCP ресурсов");

                    var response = await httpClient.GetAsync("mcp/resources", ct);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var resourcesResponse = JsonSerializer.Deserialize<McpResourcesResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var resources = resourcesResponse?.Resources ?? new List<McpResource>();
                    
                    logger.LogInformation("Получено {Count} MCP ресурсов", resources.Count);
                    return resources;

                }, TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения списка ресурсов");
                return new List<McpResource>();
            }
        }

        public async Task<string> ReadResourceAsync(string uri, CancellationToken ct = default)
        {
            try
            {
                var cacheKey = $"resource_content_{uri}";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    logger.LogDebug("Чтение ресурса: {Uri}", uri);

                    var request = new McpResourceReadRequest { Uri = uri };
                    var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("mcp/resources/read", content, ct);
                    response.EnsureSuccessStatusCode();

                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    var resourceResponse = JsonSerializer.Deserialize<McpResourceReadResponse>(responseJson, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var resourceContent = resourceResponse?.Contents?.FirstOrDefault()?.Text ?? string.Empty;
                    
                    logger.LogDebug("Прочитан ресурс {Uri}, размер: {Size} символов", uri, resourceContent.Length);
                    return resourceContent;

                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка чтения ресурса {Uri}", uri);
                return string.Empty;
            }
        }

        public async Task<bool> SubscribeToResourceAsync(string uri, CancellationToken ct = default)
        {
            try
            {
                if (subscribedResources.Contains(uri))
                {
                    logger.LogDebug("Уже подписан на ресурс {Uri}", uri);
                    return true;
                }

                logger.LogDebug("Подписка на ресурс: {Uri}", uri);

                var request = new McpResourceSubscribeRequest { Uri = uri };
                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("mcp/resources/subscribe", content, ct);
                
                if (response.IsSuccessStatusCode)
                {
                    subscribedResources.Add(uri);
                    logger.LogInformation("Успешная подписка на ресурс {Uri}", uri);
                    return true;
                }
                else
                {
                    logger.LogWarning("Не удалось подписаться на ресурс {Uri}: {StatusCode}", uri, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка подписки на ресурс {Uri}", uri);
                return false;
            }
        }

        public async Task<bool> UnsubscribeFromResourceAsync(string uri, CancellationToken ct = default)
        {
            try
            {
                if (!subscribedResources.Contains(uri))
                {
                    logger.LogDebug("Не подписан на ресурс {Uri}", uri);
                    return true;
                }

                logger.LogDebug("Отписка от ресурса: {Uri}", uri);

                var request = new McpResourceUnsubscribeRequest { Uri = uri };
                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("mcp/resources/unsubscribe", content, ct);
                
                if (response.IsSuccessStatusCode)
                {
                    subscribedResources.Remove(uri);
                    logger.LogInformation("Успешная отписка от ресурса {Uri}", uri);
                    return true;
                }
                else
                {
                    logger.LogWarning("Не удалось отписаться от ресурса {Uri}: {StatusCode}", uri, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка отписки от ресурса {Uri}", uri);
                return false;
            }
        }

        public async Task<IEnumerable<McpResource>> FindResourcesAsync(string pattern, CancellationToken ct = default)
        {
            try
            {
                var allResources = await ListResourcesAsync(ct);
                
                // Простая фильтрация по паттерну
                var matchingResources = allResources.Where(r => 
                    r.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    r.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false));

                return matchingResources.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка поиска ресурсов по паттерну {Pattern}", pattern);
                return new List<McpResource>();
            }
        }

        public async Task<string> ReadProjectFileAsync(string relativePath, CancellationToken ct = default)
        {
            try
            {
                // Формируем URI для файла проекта
                var projectFileUri = $"file://project/{relativePath.Replace('\\', '/')}";
                return await ReadResourceAsync(projectFileUri, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка чтения файла проекта {Path}", relativePath);
                return string.Empty;
            }
        }

        public void Dispose()
        {
            // Отписываемся от всех ресурсов
            foreach (var uri in subscribedResources.ToList())
            {
                try
                {
                    _ = UnsubscribeFromResourceAsync(uri, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Ошибка отписки от ресурса {Uri} при освобождении", uri);
                }
            }

            subscribedResources.Clear();
            logger.LogDebug("McpResourceService освобожден");
        }
    }

    #region DTOs

    public class McpResourcesResponse
    {
        public List<McpResource> Resources { get; set; } = new();
    }

    public class McpResourceReadRequest
    {
        public required string Uri { get; set; }
    }

    public class McpResourceReadResponse
    {
        public List<McpResourceContent> Contents { get; set; } = new();
    }

    public class McpResourceContent
    {
        public string? Uri { get; set; }
        public string? MimeType { get; set; }
        public string? Text { get; set; }
        public string? Blob { get; set; }
    }

    public class McpResourceSubscribeRequest
    {
        public required string Uri { get; set; }
    }

    public class McpResourceUnsubscribeRequest
    {
        public required string Uri { get; set; }
    }

    #endregion
}