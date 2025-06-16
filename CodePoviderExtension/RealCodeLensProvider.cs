using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;

namespace CodeProviderExtension
{
    /// <summary>
    /// Улучшенный CodeLens Provider для Visual Studio Extensibility API.
    /// </summary>
    [VisualStudioContribution]
    internal class EnhancedCodeLensProvider : ExtensionPart, ITextViewOpenClosedListener, ITextViewChangedListener
    {
        private readonly SimpleCodeLensAnalyzer analyzer;
        private readonly Dictionary<string, List<CodeLensData>> cache;
        private readonly TraceSource logger;
        private readonly Timer refreshTimer;

        public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
        {
            AppliesTo = [
                DocumentFilter.FromDocumentType("CSharp"),
                DocumentFilter.FromDocumentType("Basic"),
                DocumentFilter.FromDocumentType("C/C++")
            ],
        };

        /// <summary>
        /// Конструктор провайдера CodeLens.
        /// </summary>
        public EnhancedCodeLensProvider(ExtensionCore extensionCore, VisualStudioExtensibility extensibility, SimpleCodeLensAnalyzer analyzer) 
            : base(extensionCore, extensibility)
        {
            this.analyzer = analyzer;
            this.cache = new Dictionary<string, List<CodeLensData>>();
            this.logger = new TraceSource("EnhancedCodeLensProvider");
            
            // Таймер для периодического обновления
            this.refreshTimer = new Timer(RefreshAllCodeLens, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }        /// <summary>
        /// Обработчик открытия текстового представления.
        /// </summary>
        public Task TextViewOpenedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
        {
            try
            {
                logger.TraceInformation($"Открыт файл: {GetDocumentPath(textView)}");
                
                // Запускаем анализ в фоновом режиме
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500, cancellationToken); // Небольшая задержка для завершения загрузки
                    await AnalyzeAndDisplayCodeLensAsync(textView, cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при открытии файла: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Обработчик закрытия текстового представления.
        /// </summary>
        public async Task TextViewClosedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
        {
            try
            {
                var documentPath = GetDocumentPath(textView);
                cache.Remove(documentPath);
                logger.TraceInformation($"Закрыт файл: {documentPath}");
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при закрытии файла: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }        /// <summary>
        /// Обработчик изменения текста.
        /// </summary>
        public Task TextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
        {
            try
            {
                // Запускаем анализ с задержкой после изменений
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000, cancellationToken); // Ждем 2 секунды после последнего изменения
                    await AnalyzeAndDisplayCodeLensAsync(args.AfterTextView, cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при обработке изменений: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }        /// <summary>
        /// Анализирует код и отображает CodeLens информацию.
        /// </summary>
        private async Task AnalyzeAndDisplayCodeLensAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
        {
            try
            {
                if (!ToggleCodeLensCommand.IsCodeLensEnabled)
                    return;

                var document = textView.Document;
                if (document == null) return;                // Получаем текст из документа
                var sourceCode = string.Empty;
                try 
                {
                    // Используем правильный API для получения текста
                    sourceCode = document.Text.ToString();
                }
                catch (Exception ex)
                {
                    logger.TraceEvent(TraceEventType.Warning, 0, $"Не удалось получить текст документа: {ex.Message}");
                    return;
                }

                var documentPath = GetDocumentPath(textView);

                if (string.IsNullOrEmpty(sourceCode) || !IsSupportedFile(documentPath))
                    return;

                logger.TraceInformation($"Анализ кода для: {documentPath}");

                // Выполняем анализ
                var codeLensInfos = analyzer.AnalyzeCode(sourceCode, documentPath);
                
                // Преобразуем в наши данные и фильтруем null значения
                var codeLensData = codeLensInfos
                    .Where(info => ShouldShowCodeLens(info))
                    .Select(info => CreateCodeLensData(info, textView))
                    .Where(data => data != null)
                    .Cast<CodeLensData>() // Убираем null после проверки
                    .ToList();

                // Сохраняем в кэш
                cache[documentPath] = codeLensData;

                // Отображаем в выходном окне Visual Studio
                await DisplayCodeLensInOutputAsync(codeLensData, documentPath);

                // Показываем в статус баре
                await ShowCodeLensInStatusBarAsync(codeLensData.Count, documentPath);

                logger.TraceInformation($"Создано {codeLensData.Count} CodeLens элементов для {documentPath}");
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при анализе кода: {ex.Message}");
            }
        }        /// <summary>
        /// Создает данные CodeLens.
        /// </summary>
        private CodeLensData? CreateCodeLensData(SimpleCodeLensInfo info, ITextViewSnapshot textView)
        {
            try
            {
                // Упрощенная проверка - просто проверяем что номер строки разумный
                if (info.Line < 0)
                    return null;

                var settings = CodeLensSettings.Instance;

                return new CodeLensData
                {
                    Line = info.Line,
                    ElementName = info.ElementName,
                    Type = info.Type,
                    DisplayText = GenerateDisplayText(info, settings),
                    DetailedInfo = GenerateDetailedInfo(info),
                    Priority = info.Priority,
                    HasWarnings = info.Suggestions.Any(),
                    Suggestions = info.Suggestions,
                    Metrics = new CodeMetrics
                    {
                        CyclomaticComplexity = info.CyclomaticComplexity,
                        LinesOfCode = info.LinesOfCode,
                        ParameterCount = info.ParameterCount,
                        MethodCount = info.MethodCount,
                        PropertyCount = info.PropertyCount,
                        FieldCount = info.FieldCount
                    }
                };
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при создании CodeLens данных: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Генерирует текст для отображения.
        /// </summary>
        private string GenerateDisplayText(SimpleCodeLensInfo info, CodeLensSettings settings)
        {
            var parts = new List<string>();

            if (info.Type == CodeLensType.Method)
            {
                if (settings.ShowComplexity && info.CyclomaticComplexity > 1)
                {
                    var complexityIcon = info.CyclomaticComplexity > settings.ComplexityWarningThreshold ? "🔴" : "🟢";
                    parts.Add($"{complexityIcon} Сложность: {info.CyclomaticComplexity}");
                }
                
                if (settings.ShowLinesOfCode && info.LinesOfCode > 0)
                {
                    var linesIcon = info.LinesOfCode > settings.MaxMethodLines ? "⚠️" : "📏";
                    parts.Add($"{linesIcon} Строк: {info.LinesOfCode}");
                }
                
                if (settings.ShowParameterCount && info.ParameterCount > 0)
                {
                    var paramsIcon = info.ParameterCount > 5 ? "⚠️" : "📋";
                    parts.Add($"{paramsIcon} Параметров: {info.ParameterCount}");
                }
            }
            else if (info.Type == CodeLensType.Class && settings.ShowClassInfo)
            {
                parts.Add($"🏗️ Методов: {info.MethodCount}");
                if (info.PropertyCount > 0)
                    parts.Add($"⚙️ Свойств: {info.PropertyCount}");
                if (info.FieldCount > 0)
                    parts.Add($"📦 Полей: {info.FieldCount}");
            }

            if (settings.ShowAISuggestions && info.Suggestions.Any())
            {
                parts.Add($"💡 {info.Suggestions.Count} предложений");
            }

            return parts.Any() ? string.Join(" | ", parts) : $"📊 {info.ElementName}";
        }

        /// <summary>
        /// Генерирует детальную информацию.
        /// </summary>
        private string GenerateDetailedInfo(SimpleCodeLensInfo info)
        {
            var details = new List<string>
            {
                $"=== {info.ElementName} ({GetTypeDisplayName(info.Type)}) ==="
            };

            if (info.Type == CodeLensType.Method)
            {
                details.Add($"📊 Метрики:");
                details.Add($"  • Цикломатическая сложность: {info.CyclomaticComplexity}");
                details.Add($"  • Строк кода: {info.LinesOfCode}");
                details.Add($"  • Параметров: {info.ParameterCount}");
                
                if (info.CyclomaticComplexity > 10)
                    details.Add($"  🔴 ВНИМАНИЕ: Высокая сложность! Рекомендуется рефакторинг.");
                if (info.LinesOfCode > 50)
                    details.Add($"  ⚠️ ВНИМАНИЕ: Слишком длинный метод!");
            }
            else if (info.Type == CodeLensType.Class)
            {
                details.Add($"🏗️ Структура класса:");
                details.Add($"  • Методов: {info.MethodCount}");
                details.Add($"  • Свойств: {info.PropertyCount}");
                details.Add($"  • Полей: {info.FieldCount}");
            }

            if (info.Suggestions.Any())
            {
                details.Add($"💡 Предложения по улучшению:");
                details.AddRange(info.Suggestions.Select(s => $"  • {s}"));
            }

            return string.Join(Environment.NewLine, details);
        }

        /// <summary>
        /// Отображает CodeLens в выходном окне.
        /// </summary>
        private async Task DisplayCodeLensInOutputAsync(List<CodeLensData> codeLensData, string documentPath)
        {
            try
            {
                if (!codeLensData.Any()) return;

                var output = new List<string>
                {
                    $"",
                    $"🎯 АНАЛИЗ КОДА: {Path.GetFileName(documentPath)}",
                    $"{'='*60}",
                    $"⏰ Время анализа: {DateTime.Now:HH:mm:ss}",
                    $"📊 Найдено элементов: {codeLensData.Count}",
                    $""
                };

                foreach (var data in codeLensData.OrderBy(d => d.Line))
                {
                    output.Add($"📍 Строка {data.Line + 1}: {data.DisplayText}");
                    
                    if (data.HasWarnings)
                    {
                        output.Add($"   ⚠️ Рекомендации: {string.Join(", ", data.Suggestions)}");
                    }
                }

                output.Add($"{'='*60}");

                // Выводим все в Debug консоль
                foreach (var line in output)
                {
                    Debug.WriteLine(line);
                }

                // Можно также показать в статус баре
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при отображении CodeLens: {ex.Message}");
            }
        }

        /// <summary>
        /// Показывает информацию в статус баре.
        /// </summary>
        private async Task ShowCodeLensInStatusBarAsync(int count, string documentPath)
        {
            try
            {
                var fileName = Path.GetFileName(documentPath);
                var message = $"📊 CodeLens: {count} элементов в {fileName}";
                
                // В будущем можно интегрировать со статус баром VS
                Debug.WriteLine($"[StatusBar] {message}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при обновлении статус бара: {ex.Message}");
            }
        }

        /// <summary>
        /// Определяет, нужно ли показывать CodeLens для элемента.
        /// </summary>
        private bool ShouldShowCodeLens(SimpleCodeLensInfo info)
        {
            var settings = CodeLensSettings.Instance;
            
            if (info.Type == CodeLensType.Method)
            {
                return settings.ShowComplexity || settings.ShowLinesOfCode || settings.ShowParameterCount;
            }
            else if (info.Type == CodeLensType.Class)
            {
                return settings.ShowClassInfo;
            }

            return true;
        }

        /// <summary>
        /// Проверяет, поддерживается ли файл.
        /// </summary>
        private bool IsSupportedFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".cs" || extension == ".vb" || extension == ".cpp" || extension == ".c" || extension == ".h";
        }

        /// <summary>
        /// Получает путь к документу.
        /// </summary>
        private string GetDocumentPath(ITextViewSnapshot textView)
        {
            return textView.Document?.Uri?.LocalPath ?? "Unknown";
        }

        /// <summary>
        /// Получает отображаемое имя типа.
        /// </summary>
        private string GetTypeDisplayName(CodeLensType type)
        {
            return type switch
            {
                CodeLensType.Method => "Метод",
                CodeLensType.Class => "Класс",
                CodeLensType.Property => "Свойство",
                CodeLensType.Field => "Поле",
                CodeLensType.Interface => "Интерфейс",
                _ => "Неизвестно"
            };
        }

        /// <summary>
        /// Периодическое обновление всех CodeLens.
        /// </summary>
        private void RefreshAllCodeLens(object? state)
        {
            try
            {
                if (cache.Any())
                {
                    logger.TraceInformation($"Периодическое обновление CodeLens для {cache.Count} файлов");
                    // Здесь можно добавить логику периодического обновления
                }
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при периодическом обновлении: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождение ресурсов.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Dispose();
                cache.Clear();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Данные CodeLens для отображения.
    /// </summary>
    public class CodeLensData
    {
        public int Line { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public CodeLensType Type { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string DetailedInfo { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool HasWarnings { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public CodeMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Метрики кода.
    /// </summary>
    public class CodeMetrics
    {
        public int CyclomaticComplexity { get; set; }
        public int LinesOfCode { get; set; }
        public int ParameterCount { get; set; }
        public int MethodCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
    }
}
