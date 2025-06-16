using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodePoviderExtension.MCP
{
    /// <summary>
    /// Основная реализация MCP клиента.
    /// </summary>
    public class McpClient : IMcpClient
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<McpClient> logger;
        private readonly McpConfiguration configuration;
        private readonly McpCacheService cacheService;

        private bool isConnected;
        private bool disposed;

        public McpClient(
            HttpClient httpClient,
            ILogger<McpClient> logger,
            McpConfiguration configuration,
            McpCacheService cacheService)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

            // Создаем специфичные логгеры для каждого сервиса
            var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            var toolLogger = loggerFactory.CreateLogger<McpToolService>();
            var resourceLogger = loggerFactory.CreateLogger<McpResourceService>();
            var promptLogger = loggerFactory.CreateLogger<McpPromptService>();

            Tools = new McpToolService(httpClient, toolLogger, cacheService);
            Resources = new McpResourceService(httpClient, resourceLogger, cacheService);
            Prompts = new McpPromptService(httpClient, promptLogger, cacheService);
        }

        public bool IsConnected => isConnected && !disposed;

        public IMcpToolService Tools { get; }
        public IMcpResourceService Resources { get; }
        public IMcpPromptService Prompts { get; }

        public event EventHandler<McpConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<McpErrorEventArgs>? ErrorOccurred;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Инициализация MCP клиента...");

                // Настройка базового URL и заголовков
                httpClient.BaseAddress = new Uri(configuration.BaseUrl);
                
                if (!string.IsNullOrEmpty(configuration.ApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ApiKey}");
                }

                // Проверка соединения
                var serverInfo = await GetServerInfoAsync(ct);
                isConnected = true;

                logger.LogInformation("MCP клиент инициализирован. Сервер: {ServerName} v{Version}", 
                    serverInfo.Name, serverInfo.Version);

                OnConnectionChanged(true, "Успешная инициализация");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка инициализации MCP клиента");
                OnErrorOccurred(ex, "Инициализация");
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            try
            {
                if (isConnected)
                {
                    isConnected = false;
                    OnConnectionChanged(false, "Отключение по запросу");
                    logger.LogInformation("MCP клиент отключен");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при отключении MCP клиента");
                OnErrorOccurred(ex, "Отключение");
            }
        }

        public async Task<McpServerInfo> GetServerInfoAsync(CancellationToken ct = default)
        {
            try
            {
                var cacheKey = "server_info";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    var response = await httpClient.GetAsync("mcp/server/info", ct);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var serverInfo = JsonSerializer.Deserialize<McpServerInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return serverInfo ?? throw new InvalidOperationException("Не удалось получить информацию о сервере");
                }, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения информации о сервере");
                OnErrorOccurred(ex, "GetServerInfo");
                throw;
            }
        }

        public async Task<McpCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        {
            try
            {
                var cacheKey = "capabilities";
                return await cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    var response = await httpClient.GetAsync("mcp/server/capabilities", ct);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var capabilities = JsonSerializer.Deserialize<McpCapabilities>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return capabilities ?? new McpCapabilities();
                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения возможностей сервера");
                OnErrorOccurred(ex, "GetCapabilities");
                
                // Возвращаем базовые возможности в случае ошибки
                return new McpCapabilities
                {
                    Tools = new McpToolsCapability { ListChanged = false },
                    Resources = new McpResourcesCapability { Subscribe = false, ListChanged = false },
                    Prompts = new McpPromptsCapability { ListChanged = false }
                };
            }
        }

        protected virtual void OnConnectionChanged(bool isConnected, string? reason = null)
        {
            ConnectionChanged?.Invoke(this, new McpConnectionChangedEventArgs(isConnected, reason));
        }

        protected virtual void OnErrorOccurred(Exception exception, string context)
        {
            ErrorOccurred?.Invoke(this, new McpErrorEventArgs(exception, context));
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                isConnected = false;

                // Уведомляем об отключении
                OnConnectionChanged(false, "Освобождение ресурсов");

                // Освобождаем ресурсы сервисов
                (Tools as IDisposable)?.Dispose();
                (Resources as IDisposable)?.Dispose();
                (Prompts as IDisposable)?.Dispose();

                logger.LogInformation("MCP клиент освобожден");
            }
        }
    }

    /// <summary>
    /// Конфигурация MCP клиента.
    /// </summary>
    public class McpConfiguration
    {
        public required string BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableCaching { get; set; } = true;
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
        public bool EnableEmbeddedServer { get; set; } = true;
        public int EmbeddedServerPort { get; set; } = 3001;
    }

    /// <summary>
    /// Простой кэш сервис для MCP данных.
    /// </summary>
    public class McpCacheService
    {
        private readonly Dictionary<string, CacheEntry> cache;
        private readonly object lockObject;
        private readonly ILogger<McpCacheService> logger;

        public McpCacheService(ILogger<McpCacheService> logger)
        {
            this.cache = new Dictionary<string, CacheEntry>();
            this.lockObject = new object();
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            lock (lockObject)
            {
                if (cache.TryGetValue(key, out var entry) && !entry.IsExpired)
                {
                    logger.LogDebug("Кэш попадание для ключа: {Key}", key);
                    return (T)entry.Value;
                }
            }

            logger.LogDebug("Кэш промах для ключа: {Key}, создание нового значения", key);
            var value = await factory();

            lock (lockObject)
            {
                var cacheEntry = new CacheEntry
                {
                    Value = value!,
                    ExpiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(10))
                };
                cache[key] = cacheEntry;
            }

            return value;
        }

        public void Remove(string key)
        {
            lock (lockObject)
            {
                cache.Remove(key);
                logger.LogDebug("Удален кэш для ключа: {Key}", key);
            }
        }

        public void Clear()
        {
            lock (lockObject)
            {
                var count = cache.Count;
                cache.Clear();
                logger.LogInformation("Очищен кэш, удалено записей: {Count}", count);
            }
        }

        public void CleanupExpired()
        {
            lock (lockObject)
            {
                var expiredKeys = cache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
                
                foreach (var key in expiredKeys)
                {
                    cache.Remove(key);
                }

                if (expiredKeys.Any())
                {
                    logger.LogDebug("Удалено просроченных записей из кэша: {Count}", expiredKeys.Count);
                }
            }
        }

        private class CacheEntry
        {
            public required object Value { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }
    }
}