using System.ComponentModel;

namespace CodeProviderExtension
{
    /// <summary>
    /// Настройки для работы с AI сервисами.
    /// </summary>
    public class AISettings : INotifyPropertyChanged
    {
        private static AISettings? instance;
        private static readonly object lockObject = new object();

        public static AISettings Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        instance ??= new AISettings();
                    }
                }
                return instance;
            }
        }

        private AIProvider selectedProvider = AIProvider.OpenAI;
        private string openAIApiKey = string.Empty;
        private string openAIModel = "gpt-4";
        private string openAIEndpoint = "https://api.openai.com/v1";
        private string claudeApiKey = string.Empty;
        private string claudeModel = "claude-3-sonnet-20240229";
        private string ollamaEndpoint = "http://localhost:11434";
        private string ollamaModel = "codellama";
        private int maxTokens = 2048;
        private double temperature = 0.7;
        private int timeoutSeconds = 60;
        private bool enableLogging = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        private AISettings() { }

        /// <summary>
        /// Выбранный провайдер AI.
        /// </summary>
        public AIProvider SelectedProvider
        {
            get => selectedProvider;
            set
            {
                if (selectedProvider != value)
                {
                    selectedProvider = value;
                    OnPropertyChanged(nameof(SelectedProvider));
                }
            }
        }

        /// <summary>
        /// API ключ для OpenAI.
        /// </summary>
        public string OpenAIApiKey
        {
            get => openAIApiKey;
            set
            {
                if (openAIApiKey != value)
                {
                    openAIApiKey = value;
                    OnPropertyChanged(nameof(OpenAIApiKey));
                }
            }
        }

        /// <summary>
        /// Модель OpenAI для использования.
        /// </summary>
        public string OpenAIModel
        {
            get => openAIModel;
            set
            {
                if (openAIModel != value)
                {
                    openAIModel = value;
                    OnPropertyChanged(nameof(OpenAIModel));
                }
            }
        }

        /// <summary>
        /// Endpoint для OpenAI API.
        /// </summary>
        public string OpenAIEndpoint
        {
            get => openAIEndpoint;
            set
            {
                if (openAIEndpoint != value)
                {
                    openAIEndpoint = value;
                    OnPropertyChanged(nameof(OpenAIEndpoint));
                }
            }
        }

        /// <summary>
        /// API ключ для Claude.
        /// </summary>
        public string ClaudeApiKey
        {
            get => claudeApiKey;
            set
            {
                if (claudeApiKey != value)
                {
                    claudeApiKey = value;
                    OnPropertyChanged(nameof(ClaudeApiKey));
                }
            }
        }

        /// <summary>
        /// Модель Claude для использования.
        /// </summary>
        public string ClaudeModel
        {
            get => claudeModel;
            set
            {
                if (claudeModel != value)
                {
                    claudeModel = value;
                    OnPropertyChanged(nameof(ClaudeModel));
                }
            }
        }

        /// <summary>
        /// Endpoint для Ollama.
        /// </summary>
        public string OllamaEndpoint
        {
            get => ollamaEndpoint;
            set
            {
                if (ollamaEndpoint != value)
                {
                    ollamaEndpoint = value;
                    OnPropertyChanged(nameof(OllamaEndpoint));
                }
            }
        }

        /// <summary>
        /// Модель Ollama для использования.
        /// </summary>
        public string OllamaModel
        {
            get => ollamaModel;
            set
            {
                if (ollamaModel != value)
                {
                    ollamaModel = value;
                    OnPropertyChanged(nameof(OllamaModel));
                }
            }
        }

        /// <summary>
        /// Максимальное количество токенов в ответе.
        /// </summary>
        public int MaxTokens
        {
            get => maxTokens;
            set
            {
                if (maxTokens != value)
                {
                    maxTokens = Math.Max(1, Math.Min(8192, value));
                    OnPropertyChanged(nameof(MaxTokens));
                }
            }
        }

        /// <summary>
        /// Температура для генерации (креативность).
        /// </summary>
        public double Temperature
        {
            get => temperature;
            set
            {
                if (Math.Abs(temperature - value) > 0.01)
                {
                    temperature = Math.Max(0.0, Math.Min(2.0, value));
                    OnPropertyChanged(nameof(Temperature));
                }
            }
        }

        /// <summary>
        /// Таймаут для запросов в секундах.
        /// </summary>
        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                if (timeoutSeconds != value)
                {
                    timeoutSeconds = Math.Max(5, Math.Min(300, value));
                    OnPropertyChanged(nameof(TimeoutSeconds));
                }
            }
        }

        /// <summary>
        /// Включить подробное логирование.
        /// </summary>
        public bool EnableLogging
        {
            get => enableLogging;
            set
            {
                if (enableLogging != value)
                {
                    enableLogging = value;
                    OnPropertyChanged(nameof(EnableLogging));
                }
            }
        }

        /// <summary>
        /// Проверяет, корректно ли настроен выбранный провайдер.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                return SelectedProvider switch
                {
                    AIProvider.OpenAI => !string.IsNullOrEmpty(OpenAIApiKey),
                    AIProvider.Claude => !string.IsNullOrEmpty(ClaudeApiKey),
                    AIProvider.Ollama => !string.IsNullOrEmpty(OllamaEndpoint),
                    _ => false
                };
            }
        }

        /// <summary>
        /// Сохраняет настройки в Windows Registry или конфигурационный файл.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // TODO: Реализовать сохранение в Registry или файл настроек
                // Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CodeProviderExtension")
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }

        /// <summary>
        /// Загружает настройки из Windows Registry или конфигурационного файла.
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                // TODO: Реализовать загрузку из Registry или файла настроек
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Поддерживаемые провайдеры AI.
    /// </summary>
    public enum AIProvider
    {
        /// <summary>
        /// OpenAI GPT модели.
        /// </summary>
        OpenAI,

        /// <summary>
        /// Anthropic Claude модели.
        /// </summary>
        Claude,

        /// <summary>
        /// Локальные модели через Ollama.
        /// </summary>
        Ollama
    }
}
