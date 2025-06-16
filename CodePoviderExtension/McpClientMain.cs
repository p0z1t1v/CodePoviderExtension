using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodePoviderExtension;
using CodePoviderExtension.MCP;
using Microsoft.Extensions.Logging;

namespace CodeProviderExtension.MCP
{
    /// <summary>  
    /// Основная реализация MCP клиента.  
    /// </summary>  
    public class McpClient : IMcpClient
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<McpClient> logger;
        private McpConfiguration configuration;
        private readonly McpCacheService cacheService; // Correct namespace alignment  

        private bool isConnected;
        private bool disposed;

        public McpClient(
            HttpClient httpClient,
            ILogger<McpClient> logger,
            McpConfiguration configuration,
            McpCacheService cacheService) // Correct namespace alignment  
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
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

                httpClient.BaseAddress = new Uri(configuration.BaseUrl);

                if (!string.IsNullOrEmpty(configuration.ApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ApiKey}");
                }

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

                OnConnectionChanged(false, "Освобождение ресурсов");

                (Tools as IDisposable)?.Dispose();
                (Resources as IDisposable)?.Dispose();
                (Prompts as IDisposable)?.Dispose();

                logger.LogInformation("MCP клиент освобожден");
            }
        }
    }
}