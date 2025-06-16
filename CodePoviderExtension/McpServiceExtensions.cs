using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CodeProviderExtension.MCP;
using CodePoviderExtension.MCP;

namespace CodeProviderExtension
{
    /// <summary>
    /// Расширения для регистрации MCP сервисов в DI контейнере.
    /// </summary>
    public static class McpServiceExtensions
    {
        public static IServiceCollection AddMcpServices(this IServiceCollection services)
        {
            // Конфигурация MCP
            services.AddSingleton<McpConfiguration>(provider =>
            {
                return new McpConfiguration
                {
                    BaseUrl = Environment.GetEnvironmentVariable("MCP_BASE_URL") ?? "http://localhost:3001",
                    ApiKey = Environment.GetEnvironmentVariable("MCP_API_KEY"),
                    Timeout = 30000, // миллисекунды
                    EnableCaching = true,
                    CacheExpiration = 10 // минуты
                };
            });

            // Кэш сервис
            services.AddSingleton<McpCacheService>();

            // MCP клиент и сервисы
            services.AddSingleton<IMcpClient, CodePoviderExtension.MCP.McpClient>();
            services.AddSingleton<IMcpToolService, McpToolService>();
            services.AddSingleton<IMcpResourceService, McpResourceService>();
            services.AddSingleton<IMcpPromptService, McpPromptService>();

            // Встроенный MCP сервер
            services.AddSingleton<EmbeddedMcpServer>();

            // MCP инициализация
            services.AddSingleton<McpInitializationService>();

            return services;
        }
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

        private class CacheEntry
        {
            public required object Value { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }
    }

    /// <summary>
    /// Сервис для инициализации MCP компонентов.
    /// </summary>
    public class McpInitializationService
    {
        private readonly IMcpClient mcpClient;
        private readonly EmbeddedMcpServer embeddedServer;
        private readonly McpConfiguration configuration;
        private readonly ILogger<McpInitializationService> logger;

        public McpInitializationService(
            IMcpClient mcpClient,
            EmbeddedMcpServer embeddedServer,
            McpConfiguration configuration,
            ILogger<McpInitializationService> logger)
        {
            this.mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            this.embeddedServer = embeddedServer ?? throw new ArgumentNullException(nameof(embeddedServer));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Инициализация MCP компонентов...");

                // Запускаем встроенный сервер если включен
                if (configuration.EnableEmbeddedServer)
                {
                    logger.LogInformation("Запуск встроенного MCP сервера...");
                    await embeddedServer.StartAsync(ct);
                }

                // Инициализируем MCP клиент
                logger.LogInformation("Инициализация MCP клиента...");
                await mcpClient.InitializeAsync(ct);

                logger.LogInformation("MCP компоненты успешно инициализированы");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка инициализации MCP компонентов");
                throw;
            }
        }

        public async Task ShutdownAsync(CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Завершение работы MCP компонентов...");

                // Отключаем клиент
                if (mcpClient.IsConnected)
                {
                    await mcpClient.DisconnectAsync(ct);
                }

                // Останавливаем встроенный сервер
                if (embeddedServer.IsRunning)
                {
                    await embeddedServer.StopAsync(ct);
                }

                logger.LogInformation("MCP компоненты успешно завершены");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка завершения работы MCP компонентов");
            }
        }
    }
}
