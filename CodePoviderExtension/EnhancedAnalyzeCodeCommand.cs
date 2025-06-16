using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using CodeProviderExtension.MCP;
using CodePoviderExtension.MCP;

namespace CodeProviderExtension
{
    /// <summary>
    /// Улучшенная команда анализа кода с поддержкой MCP контекста.
    /// </summary>
    [VisualStudioContribution]
    internal class EnhancedAnalyzeCodeCommand : Command
    {
        private readonly TraceSource logger;
        private readonly ICodeAnalysisService codeAnalysisService;
        private readonly IMcpClient mcpClient;

        public EnhancedAnalyzeCodeCommand(
            VisualStudioExtensibility extensibility,
            TraceSource traceSource,
            ICodeAnalysisService codeAnalysisService,
            IMcpClient mcpClient) : base(extensibility)
        {
            this.logger = traceSource ?? throw new ArgumentNullException(nameof(traceSource));
            this.codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
            this.mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        }

        public override CommandConfiguration CommandConfiguration => new("🔍 Анализ кода (MCP)")
        {
            TooltipText = "Контекстно-осведомленный анализ кода с использованием MCP",
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu]
        };

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
        {
            try
            {
                logger.TraceInformation("Начало MCP-анализа кода");

                // Получаем активное представление
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, ct);
                if (activeTextView == null)
                {
                    await ShowErrorAsync("Нет активного документа для анализа!", ct);
                    return;
                }

                // Получаем выделенный код
                var selection = activeTextView.Selection;
                if (selection.IsEmpty)
                {
                    await ShowErrorAsync("Выделите код для анализа!", ct);
                    return;
                }

                var selectedCode = selection.Extent.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(selectedCode))
                {
                    await ShowErrorAsync("Выделенный текст пуст!", ct);
                    return;
                }

                // Определяем язык
                var documentUri = activeTextView.Document.Uri;
                var fileName = System.IO.Path.GetFileName(documentUri.LocalPath);
                var language = GetLanguageFromFileName(fileName);

                logger.TraceInformation($"MCP-анализ: {selectedCode.Length} символов на языке {language}");

                // Выполняем многоуровневый анализ
                var analysisResult = await PerformEnhancedAnalysisAsync(selectedCode, language, fileName, ct);

                // Показываем результат
                await ShowAnalysisResultAsync(analysisResult, ct);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка MCP-анализа: {ex.Message}");
                await ShowErrorAsync($"Ошибка анализа: {ex.Message}", ct);
            }
        }

        private async Task<EnhancedAnalysisResult> PerformEnhancedAnalysisAsync(
            string code, string language, string fileName, CancellationToken ct)
        {
            var result = new EnhancedAnalysisResult { FileName = fileName };

            try
            {
                // 1. Базовый анализ кода
                logger.TraceInformation("Выполняю базовый анализ кода...");
                result.BasicAnalysis = await codeAnalysisService.AnalyzeCodeAsync(code, language, ct);

                // 2. MCP контекст: получаем связанные ресурсы
                if (mcpClient.IsConnected)
                {
                    logger.TraceInformation("Получаю MCP контекст...");
                    result.McpContext = await GetMcpContextAsync(code, language, ct);

                    // 3. MCP анализ с учетом контекста
                    logger.TraceInformation("Выполняю контекстный MCP анализ...");
                    result.ContextualAnalysis = await PerformMcpAnalysisAsync(code, language, result.McpContext, ct);

                    // 4. MCP рекомендации
                    logger.TraceInformation("Генерирую MCP рекомендации...");
                    result.McpRecommendations = await GetMcpRecommendationsAsync(code, result.BasicAnalysis, ct);
                }
                else
                {
                    logger.TraceEvent(TraceEventType.Warning, 0, "MCP клиент не подключен, используется только базовый анализ");
                    result.McpContext = new McpAnalysisContext { IsAvailable = false };
                }
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при расширенном анализе: {ex.Message}");
                result.Error = ex.Message;
            }

            return result;
        }

        private async Task<McpAnalysisContext> GetMcpContextAsync(string code, string language, CancellationToken ct)
        {
            var context = new McpAnalysisContext { IsAvailable = true };

            try
            {
                // Получаем доступные ресурсы
                var resources = await mcpClient.Resources.ListResourcesAsync(ct);
                context.AvailableResources = resources.ToList();

                // Ищем релевантные файлы
                var relevantResources = await mcpClient.Resources.FindResourcesAsync($".{language}", ct);
                context.RelevantFiles = relevantResources.ToList();

                // Получаем информацию о проекте
                var projectInfo = await mcpClient.Resources.ReadResourceAsync("context://project-info", ct);
                context.ProjectInfo = projectInfo;

                logger.TraceInformation($"MCP контекст: {context.AvailableResources.Count} ресурсов, {context.RelevantFiles.Count} релевантных файлов");
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Warning, 0, $"Ошибка получения MCP контекста: {ex.Message}");
                context.Error = ex.Message;
            }

            return context;
        }

        private async Task<object> PerformMcpAnalysisAsync(string code, string language, McpAnalysisContext context, CancellationToken ct)
        {
            try
            {
                var analysisArgs = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["language"] = language,
                    ["context"] = context.ProjectInfo ?? "",
                    ["relatedFiles"] = context.RelevantFiles.Select(f => f.Name).ToArray()
                };

                return await mcpClient.Tools.CallToolAsync("analyze-code", analysisArgs, ct);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Warning, 0, $"Ошибка MCP анализа: {ex.Message}");
                return new { error = ex.Message };
            }
        }

        private async Task<McpPromptResult> GetMcpRecommendationsAsync(string code, CodeAnalysisResult basicAnalysis, CancellationToken ct)
        {
            try
            {
                var promptArgs = new Dictionary<string, object>
                {
                    ["analysisResult"] = new
                    {
                        complexity = basicAnalysis.ComplexityScore,
                        issues = basicAnalysis.Issues.Count(),
                        classes = basicAnalysis.Classes.Count(),
                        methods = basicAnalysis.Methods.Count()
                    },
                    ["codeComplexity"] = basicAnalysis.ComplexityScore,
                    ["code"] = code.Substring(0, Math.Min(code.Length, 1000)) // Ограничиваем размер
                };

                return await mcpClient.Prompts.GetPromptAsync("suggest-improvements", promptArgs, ct);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Warning, 0, $"Ошибка получения MCP рекомендаций: {ex.Message}");
                return new McpPromptResult
                {
                    Description = "Ошибка рекомендаций",
                    Messages = new List<McpPromptMessage>()
                };
            }
        }

        private async Task ShowAnalysisResultAsync(EnhancedAnalysisResult result, CancellationToken ct)
        {
            var report = BuildEnhancedReport(result);
            await this.Extensibility.Shell().ShowPromptAsync(report, PromptOptions.OK, ct);
        }

        private string BuildEnhancedReport(EnhancedAnalysisResult result)
        {
            var report = new StringBuilder();

            report.AppendLine("🔍 РАСШИРЕННЫЙ АНАЛИЗ КОДА С MCP");
            report.AppendLine($"📄 Файл: {result.FileName}");
            report.AppendLine(new string('═', 60));
            report.AppendLine();

            // Базовый анализ
            if (result.BasicAnalysis != null)
            {
                report.AppendLine("📊 БАЗОВЫЙ АНАЛИЗ:");
                report.AppendLine($"   • Язык: {result.BasicAnalysis.Language}");
                report.AppendLine($"   • Строк: {result.BasicAnalysis.LineCount}");
                report.AppendLine($"   • Символов: {result.BasicAnalysis.CharacterCount}");
                report.AppendLine($"   • Сложность: {result.BasicAnalysis.ComplexityScore:F1}/10");
                
                if (result.BasicAnalysis.Classes.Any())
                    report.AppendLine($"   • Классов: {result.BasicAnalysis.Classes.Count()}");
                if (result.BasicAnalysis.Methods.Any())
                    report.AppendLine($"   • Методов: {result.BasicAnalysis.Methods.Count()}");
                if (result.BasicAnalysis.Issues.Any())
                    report.AppendLine($"   • Проблем: {result.BasicAnalysis.Issues.Count()}");
                
                report.AppendLine();
            }

            // MCP контекст
            if (result.McpContext?.IsAvailable == true)
            {
                report.AppendLine("🌐 MCP КОНТЕКСТ:");
                report.AppendLine($"   • Доступных ресурсов: {result.McpContext.AvailableResources?.Count ?? 0}");
                report.AppendLine($"   • Релевантных файлов: {result.McpContext.RelevantFiles?.Count ?? 0}");
                
                if (!string.IsNullOrEmpty(result.McpContext.ProjectInfo))
                {
                    report.AppendLine("   • Информация о проекте: ✅");
                }
                report.AppendLine();

                // Контекстуальный анализ
                if (result.ContextualAnalysis != null)
                {
                    report.AppendLine("🎯 КОНТЕКСТУАЛЬНЫЙ АНАЛИЗ MCP:");
                    report.AppendLine("   • Анализ выполнен с учетом контекста проекта");
                    report.AppendLine("   • Учтены паттерны из релевантных файлов");
                    report.AppendLine();
                }

                // MCP рекомендации
                if (result.McpRecommendations?.Messages?.Any() == true)
                {
                    report.AppendLine("💡 MCP РЕКОМЕНДАЦИИ:");
                    foreach (var message in result.McpRecommendations.Messages.Take(3))
                    {
                        if (message.Content?.Text != null)
                        {
                            var text = message.Content.Text.Length > 100 
                                ? message.Content.Text.Substring(0, 97) + "..."
                                : message.Content.Text;
                            report.AppendLine($"   • {text}");
                        }
                    }
                    report.AppendLine();
                }
            }
            else
            {
                report.AppendLine("⚠️ MCP НЕДОСТУПЕН:");
                report.AppendLine("   • Используется только базовый анализ");
                report.AppendLine("   • Для полного анализа требуется MCP подключение");
                report.AppendLine();
            }

            // Ошибки
            if (!string.IsNullOrEmpty(result.Error))
            {
                report.AppendLine("❌ ОШИБКИ:");
                report.AppendLine($"   • {result.Error}");
                report.AppendLine();
            }

            report.AppendLine("💡 Совет: Используйте MCP для получения более точных рекомендаций");
            return report.ToString();
        }

        private async Task ShowErrorAsync(string message, CancellationToken ct)
        {
            await this.Extensibility.Shell().ShowPromptAsync($"❌ {message}", PromptOptions.OK, ct);
        }

        private string GetLanguageFromFileName(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "csharp",
                ".vb" => "vb",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".cpp" or ".cc" or ".cxx" => "cpp",
                ".c" => "c",
                _ => "text"
            };
        }
    }

    #region Support Classes

    public class EnhancedAnalysisResult
    {
        public string FileName { get; set; } = string.Empty;
        public CodeAnalysisResult? BasicAnalysis { get; set; }
        public McpAnalysisContext? McpContext { get; set; }
        public object? ContextualAnalysis { get; set; }
        public McpPromptResult? McpRecommendations { get; set; }
        public string? Error { get; set; }
    }

    public class McpAnalysisContext
    {
        public bool IsAvailable { get; set; }
        public List<McpResource> AvailableResources { get; set; } = new();
        public List<McpResource> RelevantFiles { get; set; } = new();
        public string? ProjectInfo { get; set; }
        public string? Error { get; set; }
    }

    #endregion
}