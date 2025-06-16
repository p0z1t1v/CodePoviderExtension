using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using System.Diagnostics;
using System.IO;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для демонстрации анализа текущего файла.
    /// </summary>
    [VisualStudioContribution]
    internal class DemoCodeLensCommand : Command
    {
        private readonly SimpleCodeLensAnalyzer analyzer;
        private readonly TraceSource logger;

        /// <summary>
        /// Конфигурация команды.
        /// </summary>
        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.DemoCodeLens.DisplayName%")
        {
            TooltipText = "%CodeProviderExtension.DemoCodeLens.TooltipText%",
            Icon = null,
            Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
        };

        /// <summary>
        /// Конструктор команды.
        /// </summary>
        public DemoCodeLensCommand(VisualStudioExtensibility extensibility, SimpleCodeLensAnalyzer analyzer) : base(extensibility)
        {
            this.analyzer = analyzer;
            this.logger = new TraceSource("DemoCodeLensCommand");
        }

        /// <summary>
        /// Выполнение команды демонстрации CodeLens.
        /// </summary>
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                logger.TraceInformation("Запуск демонстрации CodeLens анализа");                // Получаем активный документ
                var activeDocument = await this.Extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                
                if (activeDocument == null)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет активного документа для анализа. Откройте файл с кодом.",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }                // Получаем текст документа
                string? sourceCode = null;
                string filePath;
                
                try
                {
                    sourceCode = activeDocument.Document.Text.ToString();
                    filePath = activeDocument.Document.Uri?.LocalPath ?? "Неизвестный файл";
                    
                    if (string.IsNullOrEmpty(sourceCode))
                    {
                        await this.Extensibility.Shell().ShowPromptAsync(
                            "Документ пуст или не содержит текста.",
                            PromptOptions.OK,
                            cancellationToken);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при получении текста документа: {ex.Message}");
                    
                    await this.Extensibility.Shell().ShowPromptAsync(
                        $"Ошибка при получении текста документа: {ex.Message}",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }

                // Проверяем, что файл поддерживается
                if (!IsSupportedFile(filePath))
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        $"Файл '{Path.GetFileName(filePath)}' не поддерживается для анализа.\nПоддерживаемые форматы: .cs, .vb, .cpp, .c, .h",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }

                logger.TraceInformation($"Анализ файла: {filePath}");

                // Выполняем анализ
                var codeLensInfos = analyzer.AnalyzeCode(sourceCode, filePath);
                
                if (!codeLensInfos.Any())
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        $"В файле '{Path.GetFileName(filePath)}' не найдено элементов для анализа.",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }

                // Создаем отчет
                var report = GenerateDetailedReport(codeLensInfos, filePath);
                
                // Выводим отчет в Debug консоль
                Debug.WriteLine("=".PadRight(80, '='));
                Debug.WriteLine("🎯 ДЕМОНСТРАЦИЯ CODELENS АНАЛИЗА");
                Debug.WriteLine("=".PadRight(80, '='));
                foreach (var line in report)
                {
                    Debug.WriteLine(line);
                }
                Debug.WriteLine("=".PadRight(80, '='));

                // Показываем краткий результат пользователю
                var summary = GenerateSummary(codeLensInfos, filePath);
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    summary,
                    PromptOptions.OK,
                    cancellationToken);

                logger.TraceInformation($"Демонстрация завершена. Найдено {codeLensInfos.Count} элементов для анализа");
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при выполнении демонстрации CodeLens: {ex.Message}");
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка при анализе: {ex.Message}",
                    PromptOptions.OK,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Проверяет, поддерживается ли файл для анализа.
        /// </summary>
        private bool IsSupportedFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".cs" || extension == ".vb" || extension == ".cpp" || extension == ".c" || extension == ".h";
        }

        /// <summary>
        /// Генерирует детальный отчет по анализу.
        /// </summary>
        private List<string> GenerateDetailedReport(List<SimpleCodeLensInfo> codeLensInfos, string filePath)
        {
            var report = new List<string>
            {
                $"📄 Файл: {Path.GetFileName(filePath)}",
                $"📊 Найдено элементов: {codeLensInfos.Count}",
                $"⏰ Время анализа: {DateTime.Now:HH:mm:ss}",
                ""
            };

            var groupedByType = codeLensInfos.GroupBy(info => info.Type);

            foreach (var group in groupedByType)
            {
                report.Add($"🔸 {GetTypeDisplayName(group.Key)} ({group.Count()}):");
                
                foreach (var info in group.OrderBy(i => i.Line))
                {
                    report.Add($"   📍 Строка {info.Line + 1}: {info.ElementName}");
                    
                    if (info.Type == CodeLensType.Method)
                    {
                        report.Add($"      • Сложность: {info.CyclomaticComplexity}");
                        report.Add($"      • Строк кода: {info.LinesOfCode}");
                        report.Add($"      • Параметров: {info.ParameterCount}");
                        
                        if (info.Suggestions.Any())
                        {
                            report.Add($"      ⚠️ Рекомендации: {string.Join(", ", info.Suggestions)}");
                        }
                    }
                    else if (info.Type == CodeLensType.Class)
                    {
                        report.Add($"      • Методов: {info.MethodCount}");
                        report.Add($"      • Свойств: {info.PropertyCount}");
                        report.Add($"      • Полей: {info.FieldCount}");
                    }
                    
                    report.Add("");
                }
            }

            // Статистика
            var methods = codeLensInfos.Where(i => i.Type == CodeLensType.Method).ToList();
            var classes = codeLensInfos.Where(i => i.Type == CodeLensType.Class).ToList();

            if (methods.Any())
            {
                var avgComplexity = methods.Average(m => m.CyclomaticComplexity);
                var avgLines = methods.Average(m => m.LinesOfCode);
                var maxComplexity = methods.Max(m => m.CyclomaticComplexity);
                var complexMethods = methods.Count(m => m.CyclomaticComplexity > 10);

                report.Add("📈 СТАТИСТИКА ПО МЕТОДАМ:");
                report.Add($"   • Средняя сложность: {avgComplexity:F1}");
                report.Add($"   • Средняя длина: {avgLines:F1} строк");
                report.Add($"   • Максимальная сложность: {maxComplexity}");
                report.Add($"   • Сложных методов (>10): {complexMethods}");
                report.Add("");
            }

            if (classes.Any())
            {
                var totalMethods = classes.Sum(c => c.MethodCount);
                var totalProperties = classes.Sum(c => c.PropertyCount);
                var avgMethodsPerClass = classes.Average(c => c.MethodCount);

                report.Add("🏗️ СТАТИСТИКА ПО КЛАССАМ:");
                report.Add($"   • Общее количество методов: {totalMethods}");
                report.Add($"   • Общее количество свойств: {totalProperties}");
                report.Add($"   • Среднее методов на класс: {avgMethodsPerClass:F1}");
                report.Add("");
            }

            return report;
        }

        /// <summary>
        /// Генерирует краткую сводку для пользователя.
        /// </summary>
        private string GenerateSummary(List<SimpleCodeLensInfo> codeLensInfos, string filePath)
        {
            var methods = codeLensInfos.Where(i => i.Type == CodeLensType.Method).ToList();
            var classes = codeLensInfos.Where(i => i.Type == CodeLensType.Class).ToList();
            var warnings = codeLensInfos.SelectMany(i => i.Suggestions).Count();

            var summary = new List<string>
            {
                $"🎯 Анализ файла: {Path.GetFileName(filePath)}",
                "",
                $"📊 Результаты:",
                $"   • Классов: {classes.Count}",
                $"   • Методов: {methods.Count}",
                $"   • Предупреждений: {warnings}",
                ""
            };

            if (methods.Any())
            {
                var avgComplexity = methods.Average(m => m.CyclomaticComplexity);
                var complexMethods = methods.Count(m => m.CyclomaticComplexity > 10);
                
                summary.Add($"⚙️ Качество кода:");
                summary.Add($"   • Средняя сложность: {avgComplexity:F1}");
                
                if (complexMethods > 0)
                {
                    summary.Add($"   ⚠️ Сложных методов: {complexMethods}");
                }
                else
                {
                    summary.Add($"   ✅ Сложность в норме");
                }
                
                summary.Add("");
            }

            summary.Add("📋 Подробный отчет выведен в Debug консоль Visual Studio.");
            summary.Add("");
            summary.Add("💡 Используйте CodeLens в редакторе для интерактивного анализа!");

            return string.Join(Environment.NewLine, summary);
        }

        /// <summary>
        /// Получает отображаемое имя типа элемента.
        /// </summary>
        private string GetTypeDisplayName(CodeLensType type)
        {
            return type switch
            {
                CodeLensType.Method => "Методы",
                CodeLensType.Class => "Классы",
                CodeLensType.Property => "Свойства",
                CodeLensType.Field => "Поля",
                CodeLensType.Interface => "Интерфейсы",
                _ => "Неизвестно"
            };
        }
    }
}
