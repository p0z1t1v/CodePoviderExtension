using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для детального анализа выделенного кода.
    /// </summary>
    [VisualStudioContribution]
    internal class AnalyzeCodeCommand : Command
    {
        private readonly TraceSource logger;

        public override CommandConfiguration CommandConfiguration
        {
            get
            {
                return new("%CodeProviderExtension.AnalyzeCode.DisplayName%")
                {
                    
                    TooltipText = "%CodeProviderExtension.AnalyzeCode.TooltipText%",
                    Icon = null, // Укажите иконку, если необходимо
                    EnabledWhen = null, // Укажите условия включения, если необходимо
                    VisibleWhen = null // Укажите условия видимости, если необходимо
                };
            }
        }

        public AnalyzeCodeCommand(VisualStudioExtensibility extensibility) : base(extensibility)
        {
            this.logger = new TraceSource("AnalyzeCodeCommand");
        }

        //public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.AnalyzeCode.DisplayName%")
        //{
        //    Icon = new(ImageMoniker.KnownValues, IconSettings.IconAndText),
        //    Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        //};

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Начало детального анализа кода");

                // Получаем сервис анализа кода через сервис-локатор (упрощенный подход)
                var codeAnalysisService = new CodeAnalysisService(new HttpClient());

                // Получаем активное представление текста
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                
                if (activeTextView == null)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет активного документа для анализа!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }

                // Проверяем выделенный текст
                var selection = activeTextView.Selection;
                if (selection.IsEmpty)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Выделите код для детального анализа!",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }

                // Получаем информацию о документе
                var documentUri = activeTextView.Document.Uri;
                var fileName = System.IO.Path.GetFileName(documentUri.LocalPath);
                var fileExtension = System.IO.Path.GetExtension(fileName).TrimStart('.');

                // Получаем выделенный код
                var selectedCode = selection.Extent.ToString() ?? "";
                this.logger.TraceInformation($"Анализ выделенного кода: {selectedCode.Length} символов");

                if (string.IsNullOrWhiteSpace(selectedCode))
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Выделенный текст пуст!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }

                // Выполняем анализ кода
                var analysisResult = await codeAnalysisService.AnalyzeCodeAsync(
                    selectedCode, 
                    fileExtension, 
                    cancellationToken);

                // Получаем предложения по улучшению
                var suggestions = await codeAnalysisService.GetSuggestionsAsync(
                    selectedCode, 
                    fileExtension, 
                    cancellationToken);

                // Формируем детальный отчет
                var report = BuildDetailedReport(fileName, selectedCode, analysisResult, suggestions);

                await this.Extensibility.Shell().ShowPromptAsync(
                    report, 
                    PromptOptions.OK, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при анализе кода: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка анализа: {ex.Message}", 
                    PromptOptions.OK, 
                    cancellationToken);
            }
        }

        private static string BuildDetailedReport(string fileName, string code, CodeAnalysisResult analysisResult, IEnumerable<CodeSuggestion> suggestions)
        {
            var report = new StringBuilder();
            
            report.AppendLine($"🔍 ДЕТАЛЬНЫЙ АНАЛИЗ КОДА");
            report.AppendLine($"📄 Файл: {fileName}");
            report.AppendLine(new string('═', 50));
            report.AppendLine();
            
            // Базовая информация
            report.AppendLine("📊 МЕТРИКИ КОДА:");
            report.AppendLine($"   • Размер: {code.Length:N0} символов");
            report.AppendLine($"   • Строк: {analysisResult.LineCount:N0}");
            report.AppendLine($"   • Язык: {analysisResult.Language}");
            report.AppendLine($"   • Сложность: {analysisResult.ComplexityScore:F1}/10");
            report.AppendLine();

            // Структурный анализ
            if (analysisResult.Classes.Any() || analysisResult.Methods.Any())
            {
                report.AppendLine("🏗️ СТРУКТУРНЫЙ АНАЛИЗ:");
                
                if (analysisResult.Classes.Any())
                {
                    report.AppendLine($"   📦 Классы ({analysisResult.Classes.Count()}):");
                    foreach (var className in analysisResult.Classes.Take(10))
                    {
                        report.AppendLine($"      - {className}");
                    }
                    if (analysisResult.Classes.Count() > 10)
                    {
                        report.AppendLine($"      ... и еще {analysisResult.Classes.Count() - 10}");
                    }
                    report.AppendLine();
                }
                
                if (analysisResult.Methods.Any())
                {
                    report.AppendLine($"   🔧 Методы ({analysisResult.Methods.Count()}):");
                    foreach (var methodName in analysisResult.Methods.Take(10))
                    {
                        report.AppendLine($"      - {methodName}");
                    }
                    if (analysisResult.Methods.Count() > 10)
                    {
                        report.AppendLine($"      ... и еще {analysisResult.Methods.Count() - 10}");
                    }
                    report.AppendLine();
                }
            }

            // Проблемы
            if (analysisResult.Issues.Any())
            {
                report.AppendLine("⚠️ НАЙДЕННЫЕ ПРОБЛЕМЫ:");
                
                var groupedIssues = analysisResult.Issues.GroupBy(i => i.Severity);
                foreach (var group in groupedIssues.OrderByDescending(g => g.Key))
                {
                    var icon = group.Key switch
                    {
                        IssueSeverity.Critical => "🔴",
                        IssueSeverity.Error => "🟠",
                        IssueSeverity.Warning => "🟡",
                        IssueSeverity.Info => "🔵",
                        _ => "⚪"
                    };
                    
                    report.AppendLine($"   {icon} {group.Key} ({group.Count()}):");
                    foreach (var issue in group.Take(5))
                    {
                        if (issue.Line > 0)
                        {
                            report.AppendLine($"        Строка: {issue.Line}");
                        }
                    }
                    if (group.Count() > 5)
                    {
                        report.AppendLine($"      ... и еще {group.Count() - 5}");
                    }
                    report.AppendLine();
                }
            }
            else
            {
                report.AppendLine("✅ ПРОБЛЕМЫ НЕ ОБНАРУЖЕНЫ");
                report.AppendLine();
            }

            // Предложения по улучшению
            if (suggestions.Any())
            {
                report.AppendLine("💡 ПРЕДЛОЖЕНИЯ ПО УЛУЧШЕНИЮ:");
                foreach (var suggestion in suggestions.Take(5))
                {
           
                    
                    report.AppendLine($"      {suggestion.Description}");
                    report.AppendLine();
                }
                
                if (suggestions.Count() > 5)
                {
                    report.AppendLine($"   ... и еще {suggestions.Count() - 5} предложений");
                    report.AppendLine();
                }
            }

            report.AppendLine("💡 Совет: Используйте другие команды расширения для рефакторинга и генерации кода");

            return report.ToString();
        }
    }
}
