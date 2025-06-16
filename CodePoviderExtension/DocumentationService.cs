using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeProviderExtension
{
    /// <summary>
    /// Сервис для генерации документации к коду.
    /// </summary>
    public class DocumentationService : IDocumentationService
    {
        private readonly ILogger<DocumentationService> _logger;
        private readonly ICodeAnalysisService _codeAnalysisService;

        public DocumentationService(ILogger<DocumentationService> logger, ICodeAnalysisService codeAnalysisService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
        }

        public async Task<string> GenerateDocumentationAsync(string code, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Начинаю генерацию документации для кода на языке {Language}", language);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Получен пустой код для документирования");
                    return "Нет кода для документирования.";
                }

                // Анализируем код для понимания структуры
                var analysisResult = await _codeAnalysisService.AnalyzeCodeAsync(code, language, cancellationToken);

                var documentation = GenerateDocumentationFromAnalysis(analysisResult, code, language);
                
                _logger.LogInformation("Документация успешно сгенерирована");
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

                var explanation = await Task.Run(() => GenerateCodeExplanation(code, language), cancellationToken);
                
                _logger.LogInformation("Объяснение кода успешно сгенерировано");
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
    }
}
