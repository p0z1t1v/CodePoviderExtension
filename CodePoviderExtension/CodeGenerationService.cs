using System.Text.Json;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using CodePoviderExtension.MCP;

namespace CodeProviderExtension
{
    /// <summary>
    /// Сервис для генерации кода с использованием искусственного интеллекта.
    /// Поддерживает OpenAI API, Claude API и локальные модели.
    /// Интегрирован с MCP для контекстно-осведомленной генерации.
    /// </summary>
    internal class CodeGenerationService : ICodeGenerationService
    {
        private readonly HttpClient httpClient;
        private readonly ICodeAnalysisService codeAnalysisService;
        private readonly ILogger<CodeGenerationService> logger;
        private readonly AISettings aiSettings;
        private readonly IMcpClient? mcpClient;

        public CodeGenerationService(
            HttpClient httpClient, 
            ICodeAnalysisService codeAnalysisService,
            ILogger<CodeGenerationService> logger,
            IMcpClient? mcpClient = null)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.mcpClient = mcpClient;
            this.aiSettings = AISettings.Instance;
        }        public async Task<string> GenerateCodeAsync(string prompt, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Генерация кода для языка {Language} с помощью {Provider}", language, aiSettings.SelectedProvider);

                // 🧠 Попытка получить контекст из MCP памяти
                var mcpContext = await GetMcpContextAsync(prompt, language, cancellationToken);

                if (!aiSettings.IsConfigured)
                {
                    logger.LogWarning("AI провайдер не настроен");
                    return GenerateFallbackTemplate(prompt, language);
                }

                var enhancedPrompt = BuildEnhancedPrompt(prompt, language, mcpContext);
                
                var result = aiSettings.SelectedProvider switch
                {
                    AIProvider.OpenAI => await GenerateWithOpenAIAsync(enhancedPrompt, language, cancellationToken),
                    AIProvider.Claude => await GenerateWithClaudeAsync(enhancedPrompt, language, cancellationToken),
                    AIProvider.Ollama => await GenerateWithOllamaAsync(enhancedPrompt, language, cancellationToken),
                    _ => GenerateFallbackTemplate(prompt, language)
                };                // 💾 Сохраняем результат в MCP память для будущего использования
                if (!string.IsNullOrEmpty(result) && mcpClient?.IsConnected == true)
                {
                    await SaveGeneratedCodeToMemoryAsync(prompt, result, language, cancellationToken);
                }

                logger.LogInformation("Код успешно сгенерирован, длина: {Length} символов", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при генерации кода");
                return $"// Ошибка генерации кода: {ex.Message}\n\n{GenerateFallbackTemplate(prompt, language)}";
            }
        }

        public async Task<string> RefactorCodeAsync(string code, string instructions, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Рефакторинг кода с инструкциями: {Instructions}", instructions);

                if (!aiSettings.IsConfigured)
                {
                    logger.LogWarning("AI провайдер не настроен, применяю базовый рефакторинг");
                    var analysisResult = await codeAnalysisService.AnalyzeCodeAsync(code, "csharp", cancellationToken);
                    return ApplyBasicRefactoring(code, instructions, analysisResult);
                }

                var refactorPrompt = BuildRefactorPrompt(code, instructions);
                
                var result = aiSettings.SelectedProvider switch
                {
                    AIProvider.OpenAI => await RefactorWithOpenAIAsync(refactorPrompt, cancellationToken),
                    AIProvider.Claude => await RefactorWithClaudeAsync(refactorPrompt, cancellationToken),
                    AIProvider.Ollama => await RefactorWithOllamaAsync(refactorPrompt, cancellationToken),
                    _ => code
                };

                logger.LogInformation("Рефакторинг завершен");
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при рефакторинге");
                return $"// Ошибка рефакторинга: {ex.Message}\n\n{code}";
            }
        }

        #region AI API Methods

        private async Task<string> GenerateWithOpenAIAsync(string prompt, string language, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = aiSettings.OpenAIModel,
                messages = new[]
                {
                    new { role = "system", content = GetSystemPrompt(language) },
                    new { role = "user", content = prompt }
                },
                max_tokens = aiSettings.MaxTokens,
                temperature = aiSettings.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {aiSettings.OpenAIApiKey}");
            httpClient.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);

            var response = await httpClient.PostAsync($"{aiSettings.OpenAIEndpoint}/chat/completions", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {responseContent}");
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        private async Task<string> GenerateWithClaudeAsync(string prompt, string language, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = aiSettings.ClaudeModel,
                max_tokens = aiSettings.MaxTokens,
                messages = new[]
                {
                    new { role = "user", content = $"{GetSystemPrompt(language)}\n\n{prompt}" }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("x-api-key", aiSettings.ClaudeApiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            httpClient.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);

            var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Claude API error: {response.StatusCode} - {responseContent}");
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }

        private async Task<string> GenerateWithOllamaAsync(string prompt, string language, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = aiSettings.OllamaModel,
                prompt = $"{GetSystemPrompt(language)}\n\n{prompt}",
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            httpClient.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);

            var response = await httpClient.PostAsync($"{aiSettings.OllamaEndpoint}/api/generate", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Ollama API error: {response.StatusCode} - {responseContent}");
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return jsonResponse.GetProperty("response").GetString() ?? "";
        }

        private async Task<string> RefactorWithOpenAIAsync(string prompt, CancellationToken cancellationToken)
        {
            return await GenerateWithOpenAIAsync(prompt, "csharp", cancellationToken);
        }

        private async Task<string> RefactorWithClaudeAsync(string prompt, CancellationToken cancellationToken)
        {
            return await GenerateWithClaudeAsync(prompt, "csharp", cancellationToken);
        }

        private async Task<string> RefactorWithOllamaAsync(string prompt, CancellationToken cancellationToken)
        {
            return await GenerateWithOllamaAsync(prompt, "csharp", cancellationToken);
        }

        #endregion

        #region Prompt Building

        private string BuildRefactorPrompt(string code, string instructions)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Отрефактори следующий код согласно инструкциям:");
            prompt.AppendLine();
            prompt.AppendLine("ИНСТРУКЦИИ:");
            prompt.AppendLine(instructions);
            prompt.AppendLine();
            prompt.AppendLine("ИСХОДНЫЙ КОД:");
            prompt.AppendLine(code);
            prompt.AppendLine();
            prompt.AppendLine("Требования к рефакторингу:");
            prompt.AppendLine("- Сохрани функциональность");
            prompt.AppendLine("- Улучши читаемость и производительность");
            prompt.AppendLine("- Добавь комментарии к изменениям");
            prompt.AppendLine("- Верни только отрефакторенный код");

            return prompt.ToString();
        }

        private string GetSystemPrompt(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "csharp" or "cs" => "Ты — эксперт по C# и .NET. Создавай чистый, эффективный и хорошо документированный код следуя современным стандартам C#.",
                "javascript" or "js" => "Ты — эксперт по JavaScript. Создавай современный ES6+ код с лучшими практиками.",
                "typescript" or "ts" => "Ты — эксперт по TypeScript. Создавай типобезопасный код с полными определениями типов.",
                "python" or "py" => "Ты — эксперт по Python. Следуй PEP 8 и создавай питонический код.",
                _ => $"Ты — эксперт программист. Создавай качественный код на языке {language}."
            };
        }

        #endregion

        #region Fallback Templates

        private string GenerateFallbackTemplate(string prompt, string language)
        {
            return language.ToLowerInvariant() switch
            {
                "csharp" or "cs" => GenerateCSharpTemplate(prompt),
                "javascript" or "js" => GenerateJavaScriptTemplate(prompt),
                "typescript" or "ts" => GenerateTypeScriptTemplate(prompt),
                "python" or "py" => GeneratePythonTemplate(prompt),
                _ => GenerateGenericTemplate(prompt, language)
            };
        }

        private string GenerateCSharpTemplate(string prompt)
        {
            if (prompt.Contains("класс", StringComparison.OrdinalIgnoreCase) || 
                prompt.Contains("class", StringComparison.OrdinalIgnoreCase))
            {
                return CodeTemplates.GenerateClassTemplate(prompt);
            }
            
            if (prompt.Contains("метод", StringComparison.OrdinalIgnoreCase) || 
                prompt.Contains("method", StringComparison.OrdinalIgnoreCase))
            {
                return CodeTemplates.GenerateMethodTemplate(prompt);
            }
            
            return CodeTemplates.GenerateGenericCSharpTemplate(prompt);
        }

        private string GenerateJavaScriptTemplate(string prompt)
        {
            return $@"// Сгенерированный JavaScript код на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

function generatedFunction() {{
    // TODO: Добавьте код здесь
    console.log('JavaScript код сгенерирован успешно!');
}}

// Экспорт функции (если используется модульная система)
// module.exports = generatedFunction;";
        }

        private string GenerateTypeScriptTemplate(string prompt)
        {
            return $@"// Сгенерированный TypeScript код на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

interface GeneratedInterface {{
    // TODO: Определите интерфейс
}}

function generatedFunction(): void {{
    // TODO: Добавьте код здесь
    console.log('TypeScript код сгенерирован успешно!');
}}

export {{ generatedFunction }};";
        }        private string GeneratePythonTemplate(string prompt)
        {
            return $@"# Сгенерированный Python код на основе запроса: {prompt}
# TODO: Реализуйте требуемую функциональность

def generated_function():
    """"""
    TODO: Добавьте описание функции
    """"""
    # TODO: Добавьте код здесь
    print('Python код сгенерирован успешно!')

if __name__ == '__main__':
    generated_function()";
        }

        private string GenerateGenericTemplate(string prompt, string language)
        {
            return $@"// Сгенерированный код ({language}) на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

// Добавьте ваш код здесь
// Код успешно сгенерирован для языка: {language}";
        }

        #endregion

        #region Basic Refactoring

        private string ApplyBasicRefactoring(string code, string instructions, CodeAnalysisResult analysisResult)
        {
            var refactoredCode = code;
            var instructionsLower = instructions.ToLowerInvariant();

            // Применяем различные типы рефакторинга на основе инструкций
            if (instructionsLower.Contains("производительность") || instructionsLower.Contains("performance"))
            {
                refactoredCode = ApplyPerformanceRefactoring(refactoredCode);
            }

            if (instructionsLower.Contains("читаемость") || instructionsLower.Contains("readability"))
            {
                refactoredCode = ApplyReadabilityRefactoring(refactoredCode);
            }

            if (instructionsLower.Contains("обработка ошибок") || instructionsLower.Contains("error handling"))
            {
                refactoredCode = AddErrorHandling(refactoredCode);
            }

            if (instructionsLower.Contains("упростить") || instructionsLower.Contains("simplify"))
            {
                refactoredCode = SimplifyCode(refactoredCode);
            }

            // Добавляем комментарий о примененных изменениях
            var header = $"// Рефакторинг применен: {instructions}\n// Обнаружено проблем: {analysisResult.Issues.Count()}\n\n";
            
            return header + refactoredCode;
        }

        private string ApplyPerformanceRefactoring(string code)
        {
            // Простые оптимизации производительности
            code = code.Replace("string += ", "stringBuilder.Append(");
            code = code.Replace("new List<>()", "new List<>() // TODO: Укажите начальную емкость если известна");
            
            return code;
        }

        private string ApplyReadabilityRefactoring(string code)
        {
            // Улучшения читаемости
            var lines = code.Split('\n');
            var result = new StringBuilder();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Добавляем комментарии к сложным строкам
                if (trimmedLine.Contains("&&") || trimmedLine.Contains("||"))
                {
                    result.AppendLine("        // TODO: Рассмотрите вынесение условия в отдельную переменную");
                }
                
                result.AppendLine(line);
            }
            
            return result.ToString();
        }

        private string AddErrorHandling(string code)
        {
            // Базовое добавление обработки ошибок
            if (!code.Contains("try") && !code.Contains("catch"))
            {
                return $@"try
{{
{code}
}}
catch (Exception ex)
{{
    // TODO: Добавьте соответствующую обработку ошибок
    throw new InvalidOperationException(""Операция не может быть выполнена"", ex);
}}";
            }
            
            return code;
        }

        private string SimplifyCode(string code)
        {
            // Упрощение кода
            code = code.Replace("if (condition == true)", "if (condition)");
            code = code.Replace("if (condition == false)", "if (!condition)");
            code = code.Replace("?.ToString() ?? \"\"", "?.ToString() ?? string.Empty");
            
            return code;
        }

        #endregion

        #region MCP Integration        /// <summary>
        /// Получает контекст из MCP памяти для улучшения генерации кода
        /// </summary>
        private async Task<string> GetMcpContextAsync(string prompt, string language, CancellationToken cancellationToken)
        {
            if (mcpClient?.IsConnected != true)
                return string.Empty;

            try
            {
                // Ищем похожий код в памяти проекта
                var searchRequest = new Dictionary<string, object>
                {
                    ["query"] = prompt,
                    ["projectId"] = "CodeProviderExtension",
                    ["maxResults"] = 3,
                    ["artifactType"] = "CodeSnippet"
                };                var searchResult = await mcpClient.Tools.CallToolAsync("SearchProjectArtifacts", searchRequest, cancellationToken);
                var searchResultString = searchResult?.ToString();
                
                if (!string.IsNullOrEmpty(searchResultString))
                {
                    logger.LogInformation("Найден контекст из MCP памяти для промпта: {Prompt}", prompt);
                    return $"\n\n--- Контекст из памяти проекта ---\n{searchResultString}\n--- Конец контекста ---\n";
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Не удалось получить контекст из MCP: {Error}", ex.Message);
            }

            return string.Empty;
        }

        /// <summary>
        /// Сохраняет сгенерированный код в MCP память
        /// </summary>
        private async Task SaveGeneratedCodeToMemoryAsync(string prompt, string code, string language, CancellationToken cancellationToken)
        {
            if (mcpClient?.IsConnected != true)
                return;

            try
            {
                var saveRequest = new Dictionary<string, object>
                {
                    ["title"] = $"Сгенерированный {language} код: {prompt.Substring(0, Math.Min(prompt.Length, 50))}...",
                    ["content"] = code,
                    ["projectId"] = "CodeProviderExtension",
                    ["type"] = "CodeSnippet",
                    ["tags"] = $"generated,{language},ai",
                    ["metadata"] = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        originalPrompt = prompt,
                        language = language,
                        generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        provider = aiSettings.SelectedProvider.ToString()
                    })
                };

                await mcpClient.Tools.CallToolAsync("SaveProjectArtifact", saveRequest, cancellationToken);
                logger.LogInformation("Сгенерированный код сохранен в MCP память");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Не удалось сохранить код в MCP: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Строит улучшенный промпт с учетом MCP контекста
        /// </summary>
        private string BuildEnhancedPrompt(string originalPrompt, string language, string mcpContext = "")
        {
            var enhancedPrompt = new StringBuilder();
            
            enhancedPrompt.AppendLine($"Сгенерируй {language} код для следующего запроса:");
            enhancedPrompt.AppendLine($"Запрос: {originalPrompt}");
            enhancedPrompt.AppendLine();
            
            if (!string.IsNullOrEmpty(mcpContext))
            {
                enhancedPrompt.AppendLine("Используй следующий контекст из памяти проекта:");
                enhancedPrompt.AppendLine(mcpContext);
                enhancedPrompt.AppendLine();
            }
            
            enhancedPrompt.AppendLine("Требования:");
            enhancedPrompt.AppendLine("- Код должен быть чистым и читаемым");
            enhancedPrompt.AppendLine("- Добавь комментарии для сложных участков");
            enhancedPrompt.AppendLine("- Используй лучшие практики языка");
            enhancedPrompt.AppendLine("- Обработай возможные ошибки");
            enhancedPrompt.AppendLine();
            enhancedPrompt.AppendLine("Возвращай только код без дополнительного текста.");

            return enhancedPrompt.ToString();
        }

        #endregion
    }
}
