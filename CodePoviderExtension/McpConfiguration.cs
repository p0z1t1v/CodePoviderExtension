using System;

namespace CodeProviderExtension
{
    /// <summary>
    /// Конфигурация для MCP (Model Context Protocol) клиента
    /// </summary>
    public class McpConfiguration
    {        /// <summary>
        /// URL сервера MCP
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:3000";

        /// <summary>
        /// Базовый URL (алиас для ServerUrl для совместимости)
        /// </summary>
        public string BaseUrl 
        { 
            get => ServerUrl; 
            set => ServerUrl = value; 
        }        /// <summary>
        /// Таймаут подключения в миллисекундах
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Таймаут запроса в миллисекундах (алиас для ConnectionTimeoutMs)
        /// </summary>
        public int Timeout 
        { 
            get => ConnectionTimeoutMs; 
            set => ConnectionTimeoutMs = value; 
        }

        /// <summary>
        /// Таймаут запроса в миллисекундах
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Включить кэширование
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Время жизни кэша в минутах
        /// </summary>
        public int CacheExpiration { get; set; } = 60;

        /// <summary>
        /// Включить встроенный сервер
        /// </summary>
        public bool EnableEmbeddedServer { get; set; } = false;

        /// <summary>
        /// Максимальное количество попыток переподключения
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Задержка между попытками переподключения в миллисекундах
        /// </summary>
        public int RetryDelayMs { get; set; } = 5000;

        /// <summary>
        /// Использовать ли SSL соединение
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// API ключ для аутентификации (если требуется)
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Дополнительные заголовки для HTTP запросов
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Включить подробное логирование
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// Путь к сертификату (если используется кастомный SSL)
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Валидирует конфигурацию
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
                return false;

            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
                return false;

            if (ConnectionTimeoutMs <= 0 || RequestTimeoutMs <= 0)
                return false;

            if (MaxRetryAttempts < 0 || RetryDelayMs < 0)
                return false;

            return true;
        }

        /// <summary>
        /// Создает конфигурацию по умолчанию
        /// </summary>
        public static McpConfiguration Default => new McpConfiguration();

        /// <summary>
        /// Создает конфигурацию для локальной разработки
        /// </summary>
        public static McpConfiguration LocalDevelopment => new McpConfiguration
        {
            ServerUrl = "http://localhost:3000",
            ConnectionTimeoutMs = 10000,
            RequestTimeoutMs = 30000,
            EnableVerboseLogging = true
        };

        /// <summary>
        /// Создает конфигурацию для продакшена
        /// </summary>
        public static McpConfiguration Production => new McpConfiguration
        {
            ServerUrl = "https://api.example.com",
            ConnectionTimeoutMs = 30000,
            RequestTimeoutMs = 60000,
            UseSsl = true,
            EnableVerboseLogging = false,
            MaxRetryAttempts = 5
        };
    }
}
