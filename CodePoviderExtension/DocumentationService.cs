using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace CodeProviderExtension
{
    /// <summary>
    /// Сервис для генерации документации к коду с поддержкой AI.
    /// </summary>
    public class DocumentationService : IDocumentationService
    {
        private readonly ILogger<DocumentationService> _logger;
        private readonly ICodeAnalysisService _codeAnalysisService;
        private readonly HttpClient _httpClient;
        private readonly AISettings _aiSettings;

        public DocumentationService(
            ILogger<DocumentationService> logger, 
            ICodeAnalysisService codeAnalysisService,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _aiSettings = AISettings.Instance;
        }        public async Task<string> GenerateDocumentationAsync(string code, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Начинаю генерацию документации для кода на языке {Language}", language);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Получен пустой код для документирования");
                    return "Нет кода для документирования.";
                }

                // Пытаемся использовать AI для генерации документации
                if (_aiSettings.IsConfigured)
                {
                    var aiDocumentation = await GenerateAIDocumentationAsync(code, language, cancellationToken);
                    if (!string.IsNullOrEmpty(aiDocumentation))
                    {
                        _logger.LogInformation("Документация успешно сгенерирована с помощью AI");
                        return aiDocumentation;
                    }
                }

                // Fallback: генерируем документацию на основе анализа
                var analysisResult = await _codeAnalysisService.AnalyzeCodeAsync(code, language, cancellationToken);
                var documentation = GenerateDocumentationFromAnalysis(analysisResult, code, language);
                
                _logger.LogInformation("Документация успешно сгенерирована на основе анализа");
                return documentation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации документации");
                return $"Ошибка при генерации документации: {ex.Message}";
            }
        }        public async Task<string> ExplainCodeAsync(string code, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Начинаю объяснение кода на языке {Language}", language);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Получен пустой код для объяснения");
                    return "Нет кода для объяснения.";
                }

                // Пытаемся использовать AI для объяснения кода
                if (_aiSettings.IsConfigured)
                {
                    var aiExplanation = await GenerateAIExplanationAsync(code, language, cancellationToken);
                    if (!string.IsNullOrEmpty(aiExplanation))
                    {
                        _logger.LogInformation("Объяснение кода успешно сгенерировано с помощью AI");
                        return aiExplanation;
                    }
                }

                // Fallback: простое объяснение на основе структуры
                var explanation = await Task.Run(() => GenerateCodeExplanation(code, language), cancellationToken);
                
                _logger.LogInformation("Объяснение кода успешно сгенерировано на основе анализа");
                return explanation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при объяснении кода");
                return $"Ошибка при объяснении кода: {ex.Message}";
            }
        }

        private string GenerateDocumentationFromAnalysis(CodeAnalysisResult analysisResult, string code, string language)
        {
            var documentation = $"# Документация кода\n\n";
            documentation += $"**Язык:** {analysisResult.Language}\n";
            documentation += $"**Количество строк:** {analysisResult.LineCount}\n";
            documentation += $"**Количество символов:** {analysisResult.CharacterCount}\n";
            documentation += $"**Оценка сложности:** {analysisResult.ComplexityScore:F2}\n\n";

            if (analysisResult.Classes.Any())
            {
                documentation += "## Классы\n";
                foreach (var className in analysisResult.Classes)
                {
                    documentation += $"- `{className}`\n";
                }
                documentation += "\n";
            }

            if (analysisResult.Methods.Any())
            {
                documentation += "## Методы\n";
                foreach (var methodName in analysisResult.Methods)
                {
                    documentation += $"- `{methodName}`\n";
                }
                documentation += "\n";
            }

            if (analysisResult.Issues.Any())
            {
                documentation += "## Обнаруженные проблемы\n";
                foreach (var issue in analysisResult.Issues)
                {
                    documentation += $"- **{issue.Severity}** (строка {issue.Line}): {issue.Message}\n";
                }
                documentation += "\n";
            }

            documentation += "## Рекомендации\n";
            documentation += GenerateRecommendations(analysisResult);

            return documentation;
        }

        private string GenerateCodeExplanation(string code, string language)
        {
            var explanation = $"# Объяснение кода ({language})\n\n";
            
            // Простое объяснение на основе анализа структуры
            var lines = code.Split('\n');
            explanation += $"Данный код содержит {lines.Length} строк.\n\n";

            // Анализируем ключевые элементы
            if (language.ToLower() == "csharp")
            {
                explanation += AnalyzeCSharpStructure(code);
            }
            else
            {
                explanation += "Код содержит инструкции на языке " + language + ".\n";
                explanation += "Для более детального анализа рекомендуется использовать специализированные инструменты.\n";
            }

            return explanation;
        }

        private string AnalyzeCSharpStructure(string code)
        {
            var explanation = "";
            
            if (code.Contains("class "))
                explanation += "- Содержит определения классов\n";
            
            if (code.Contains("interface "))
                explanation += "- Содержит определения интерфейсов\n";
            
            if (code.Contains("public ") || code.Contains("private ") || code.Contains("protected "))
                explanation += "- Использует модификаторы доступа\n";
            
            if (code.Contains("async ") || code.Contains("await "))
                explanation += "- Использует асинхронное программирование\n";
            
            if (code.Contains("try") && code.Contains("catch"))
                explanation += "- Содержит обработку исключений\n";

            return explanation;
        }

        private string GenerateRecommendations(CodeAnalysisResult analysisResult)
        {
            var recommendations = "";
            
            if (analysisResult.ComplexityScore > 7.0)
            {
                recommendations += "- Рассмотрите возможность рефакторинга для снижения сложности\n";
            }
            
            if (analysisResult.LineCount > 100)
            {
                recommendations += "- Рассмотрите разбиение кода на более мелкие модули\n";
            }
            
            if (analysisResult.Issues.Any(i => i.Severity == IssueSeverity.Error))
            {
                recommendations += "- Исправьте критические ошибки перед использованием кода\n";
            }

            if (string.IsNullOrEmpty(recommendations))
            {
                recommendations = "- Код выглядит хорошо структурированным\n";
            }

            return recommendations;
        }

        #region AI Documentation Methods

        private async Task<string> GenerateAIDocumentationAsync(string code, string language, CancellationToken cancellationToken)
        {
            try
            {
                var prompt = BuildDocumentationPrompt(code, language);
                
                return _aiSettings.SelectedProvider switch
                {
                    AIProvider.OpenAI => await GenerateDocumentationWithOpenAIAsync(prompt, cancellationToken),
                    AIProvider.Claude => await GenerateDocumentationWithClaudeAsync(prompt, cancellationToken),
                    AIProvider.Ollama => await GenerateDocumentationWithOllamaAsync(prompt, cancellationToken),
                    _ => string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при генерации документации с помощью AI");
                return string.Empty;
            }
        }

        private async Task<string> GenerateDocumentationWithOpenAIAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.OpenAIModel,
                messages = new[]
                {
                    new { role = "system", content = "Ты эксперт по созданию технической документации. Создавай подробную, структурированную документацию для кода." },
                    new { role = "user", content = prompt }
                },
                max_tokens = _aiSettings.MaxTokens,
                temperature = 0.3 // Низкая температура для более точной документации
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiSettings.OpenAIApiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(_aiSettings.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{_aiSettings.OpenAIEndpoint}/chat/completions", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI API вернул ошибку: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        private async Task<string> GenerateDocumentationWithClaudeAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.ClaudeModel,
                max_tokens = _aiSettings.MaxTokens,
                messages = new[]
                {
                    new { role = "user", content = $"Создай подробную техническую документацию для следующего кода:\n\n{prompt}" }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _aiSettings.ClaudeApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.Timeout = TimeSpan.FromSeconds(_aiSettings.TimeoutSeconds);

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Claude API вернул ошибку: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        private async Task<string> GenerateDocumentationWithOllamaAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.OllamaModel,
                prompt = $"Создай подробную техническую документацию для следующего кода:\n\n{prompt}",
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.Timeout = TimeSpan.FromSeconds(_aiSettings.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{_aiSettings.OllamaEndpoint}/api/generate", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama API вернул ошибку: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("response").GetString() ?? string.Empty;
        }

        private string BuildDocumentationPrompt(string code, string language)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine($"Создай подробную техническую документацию для следующего {language} кода:");
            prompt.AppendLine();
            prompt.AppendLine("```" + language);
            prompt.AppendLine(code);
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("Включи в документацию:");
            prompt.AppendLine("1. Краткое описание назначения кода");
            prompt.AppendLine("2. Описание основных классов и методов");
            prompt.AppendLine("3. Параметры и возвращаемые значения");
            prompt.AppendLine("4. Примеры использования (если применимо)");
            prompt.AppendLine("5. Особенности реализации");
            prompt.AppendLine("6. Рекомендации по использованию");
            prompt.AppendLine();
            prompt.AppendLine("Формат: Markdown с заголовками и структурированными разделами.");

            return prompt.ToString();
        }

        #endregion

        #region AI Explanation Methods

        private async Task<string> GenerateAIExplanationAsync(string code, string language, CancellationToken cancellationToken)
        {
            try
            {
                var prompt = BuildExplanationPrompt(code, language);
                
                return _aiSettings.SelectedProvider switch
                {
                    AIProvider.OpenAI => await GenerateExplanationWithOpenAIAsync(prompt, cancellationToken),
                    AIProvider.Claude => await GenerateExplanationWithClaudeAsync(prompt, cancellationToken),
                    AIProvider.Ollama => await GenerateExplanationWithOllamaAsync(prompt, cancellationToken),
                    _ => string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при генерации объяснения с помощью AI");
                return string.Empty;
            }
        }

        private async Task<string> GenerateExplanationWithOpenAIAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.OpenAIModel,
                messages = new[]
                {
                    new { role = "system", content = "Ты эксперт-программист. Объясняй код простым и понятным языком, как если бы говорил с начинающим разработчиком." },
                    new { role = "user", content = prompt }
                },
                max_tokens = _aiSettings.MaxTokens,
                temperature = 0.4
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiSettings.OpenAIApiKey}");
            _httpClient.Timeout = TimeSpan.FromSeconds(_aiSettings.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{_aiSettings.OpenAIEndpoint}/chat/completions", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        private async Task<string> GenerateExplanationWithClaudeAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.ClaudeModel,
                max_tokens = _aiSettings.MaxTokens,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _aiSettings.ClaudeApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        private async Task<string> GenerateExplanationWithOllamaAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = _aiSettings.OllamaModel,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_aiSettings.OllamaEndpoint}/api/generate", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("response").GetString() ?? string.Empty;
        }

        private string BuildExplanationPrompt(string code, string language)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine($"Объясни следующий {language} код простым и понятным языком:");
            prompt.AppendLine();
            prompt.AppendLine("```" + language);
            prompt.AppendLine(code);
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("Включи в объяснение:");
            prompt.AppendLine("1. Что делает этот код");
            prompt.AppendLine("2. Как он работает (пошагово)");
            prompt.AppendLine("3. Какие технологии/паттерны используются");
            prompt.AppendLine("4. Возможные улучшения или проблемы");
            prompt.AppendLine();
            prompt.AppendLine("Объясняй так, чтобы было понятно разработчику любого уровня.");

            return prompt.ToString();
        }

        #endregion
    }
}
