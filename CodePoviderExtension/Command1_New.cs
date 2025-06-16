using System.Diagnostics;
using System.Text;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для быстрого анализа кода из активного документа редактора.
    /// </summary>
    [VisualStudioContribution]
    internal class QuickAnalyzeCommand : Command
    {
        private readonly TraceSource logger;
        private readonly ICodeAnalysisService codeAnalysisService;

        public QuickAnalyzeCommand(TraceSource traceSource, ICodeAnalysisService codeAnalysisService)
        {
            this.logger = Requires.NotNull(traceSource, nameof(traceSource));
            this.codeAnalysisService = Requires.NotNull(codeAnalysisService, nameof(codeAnalysisService));
        }

        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.QuickAnalyze.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.DocumentOutline, IconSettings.IconAndText),
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        };

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Начало быстрого анализа кода");

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

                // Получаем информацию о документе
                var documentUri = activeTextView.Document.Uri;
                var fileName = System.IO.Path.GetFileName(documentUri.LocalPath);
                var fileExtension = System.IO.Path.GetExtension(fileName).TrimStart('.');

                // Получаем текст документа
                string documentText;
                var selection = activeTextView.Selection;
                
                if (!selection.IsEmpty)
                {
                    // Если есть выделенный текст, анализируем его
                    documentText = selection.Extent.ToString() ?? "";
                    this.logger.TraceInformation($"Анализ выделенного текста: {documentText.Length} символов");
                }
                else
                {
                    // Если нет выделения, предлагаем выделить весь документ
                    var result = await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет выделенного текста. Выделить весь документ для анализа?", 
                        PromptOptions.OKCancel, 
                        cancellationToken);
                    
                    if (result)
                    {
                        return;
                    }

                    // Используем редактор для выделения всего текста
                    await this.Extensibility.Editor().EditAsync(
                        batch =>
                        {
                            var editor = activeTextView.AsEditable(batch);
                        }, 
                        cancellationToken);

                    // Теперь получаем выделенный текст
                    var newSelection = activeTextView.Selection;
                    documentText = newSelection.Extent.ToString() ?? "";
                    this.logger.TraceInformation($"Анализ всего документа: {documentText.Length} символов");
                }

                if (string.IsNullOrWhiteSpace(documentText))
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Не удалось получить текст для анализа!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }

                // Выполняем быстрый анализ кода
                var analysisResult = await this.codeAnalysisService.AnalyzeCodeAsync(
                    documentText, 
                    fileExtension, 
                    cancellationToken);

                // Формируем краткий отчет
                var report = BuildQuickReport(fileName, documentText, analysisResult);

                await this.Extensibility.Shell().ShowPromptAsync(
                    report, 
                    PromptOptions.OK, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при анализе: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка анализа: {ex.Message}", 
                    PromptOptions.OK, 
                    cancellationToken);
            }
        }

        private static string BuildQuickReport(string fileName, string documentText, CodeAnalysisResult analysisResult)
        {
            var report = new StringBuilder();
            
            report.AppendLine($"📄 ДОКУМЕНТ: {fileName}");
            report.AppendLine(new string('─', 40));
            report.AppendLine();
            
            // Базовая информация
            report.AppendLine("📊 ОСНОВНАЯ ИНФОРМАЦИЯ:");
            report.AppendLine($"   • Размер: {documentText.Length:N0} символов");
            report.AppendLine($"   • Строк: {analysisResult.LineCount:N0}");
            report.AppendLine($"   • Язык: {analysisResult.Language}");
            report.AppendLine();

            // Структура кода
            if (analysisResult.Classes.Any() || analysisResult.Methods.Any())
            {
                report.AppendLine("🏗️ СТРУКТУРА КОДА:");
                
                if (analysisResult.Classes.Any())
                {
                    report.AppendLine($"   • Классов: {analysisResult.Classes.Count()}");
                    if (analysisResult.Classes.Count() <= 3)
                    {
                        foreach (var className in analysisResult.Classes)
                        {
                            report.AppendLine($"     - {className}");
                        }
                    }
                }
                
                if (analysisResult.Methods.Any())
                {
                    report.AppendLine($"   • Методов: {analysisResult.Methods.Count()}");
                }
                
                report.AppendLine();
            }

            // Качество кода
            var qualityIndicator = analysisResult.ComplexityScore switch
            {
                <= 2.0 => "🟢 Простой",
                <= 4.0 => "🟡 Умеренный",
                <= 6.0 => "🟠 Сложный",
                _ => "🔴 Очень сложный"
            };
            
            report.AppendLine("🎯 ОЦЕНКА СЛОЖНОСТИ:");
            report.AppendLine($"   • Уровень: {qualityIndicator} ({analysisResult.ComplexityScore:F1}/10)");
            
            // Проблемы (краткий обзор)
            if (analysisResult.Issues.Any())
            {
                var criticalCount = analysisResult.Issues.Count(i => i.Severity == IssueSeverity.Critical);
                var errorCount = analysisResult.Issues.Count(i => i.Severity == IssueSeverity.Error);
                var warningCount = analysisResult.Issues.Count(i => i.Severity == IssueSeverity.Warning);
                
                report.AppendLine($"   • Проблем найдено: {analysisResult.Issues.Count()}");
                
                if (criticalCount > 0) report.AppendLine($"     🔴 Критических: {criticalCount}");
                if (errorCount > 0) report.AppendLine($"     🟠 Ошибок: {errorCount}");
                if (warningCount > 0) report.AppendLine($"     🟡 Предупреждений: {warningCount}");
            }
            else
            {
                report.AppendLine("   • ✅ Проблем не обнаружено");
            }
            
            report.AppendLine();

            // Первые строки кода для предварительного просмотра
            report.AppendLine("📖 ПРЕДВАРИТЕЛЬНЫЙ ПРОСМОТР:");
            var lines = documentText.Split('\n');
            var previewLines = lines.Take(5);
            
            foreach (var line in previewLines)
            {
                var trimmedLine = line.Length > 60 ? line.Substring(0, 57) + "..." : line;
                report.AppendLine($"   {trimmedLine}");
            }
            
            if (lines.Length > 5)
            {
                report.AppendLine($"   ... (еще {lines.Length - 5} строк)");
            }

            report.AppendLine();
            report.AppendLine("💡 Совет: Выделите конкретный участок кода для детального анализа");

            return report.ToString();
        }
    }
}
