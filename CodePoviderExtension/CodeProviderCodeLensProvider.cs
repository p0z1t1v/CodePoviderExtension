

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace CodeProviderExtension
{    /// <summary>
    /// УСТАРЕВШИЙ провайдер CodeLens - заменен на EnhancedCodeLensProvider.
    /// Оставлен для совместимости.
    /// </summary>
    // [VisualStudioContribution] - ОТКЛЮЧЕН
    internal class CodeProviderCodeLensProvider : ExtensionPart, ITextViewOpenClosedListener, ITextViewChangedListener
    {
        private readonly SimpleCodeLensAnalyzer codeLensAnalyzer;
        private readonly Dictionary<string, List<CodeLensInfo>> codeLensCache;

        public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
        {
            AppliesTo = [ DocumentFilter.FromDocumentType("code") ],
        };

        /// <summary>
        /// Конструктор провайдера CodeLens.
        /// </summary>
        public CodeProviderCodeLensProvider(ExtensionCore extensionCore, VisualStudioExtensibility extensibility, SimpleCodeLensAnalyzer codeLensAnalyzer) 
            : base(extensionCore, extensibility)
        {
            this.codeLensAnalyzer = codeLensAnalyzer;
            this.codeLensCache = new Dictionary<string, List<CodeLensInfo>>();
        }

        /// <summary>
        /// Обработчик открытия текстового представления.
        /// </summary>
        public Task TextViewOpenedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    UpdateCodeLens(textView);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении CodeLens: {ex.Message}");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Обработчик закрытия текстового представления.
        /// </summary>
        public Task TextViewClosedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
        {
            var documentUri = textView.Document.Uri.ToString();
            codeLensCache.Remove(documentUri);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Обработчик изменения текста.
        /// </summary>
        public Task TextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);
                    UpdateCodeLens(args.AfterTextView);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении CodeLens после изменений: {ex.Message}");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }        private void UpdateCodeLens(ITextViewSnapshot textView)
        {
            try
            {
                var document = textView.Document;
                if (document == null) return;

                var sourceCode = textView.Selection.ToString();
                var fileName = document.Uri.ToString();

                var language = GetLanguageFromFileName(fileName);
                if (string.IsNullOrEmpty(language) || !IsSupportedLanguage(language))
                    return;                // Используем упрощенный анализатор
                var codeLensInfos = this.codeLensAnalyzer.AnalyzeCode(sourceCode, fileName);

                codeLensCache[fileName] = codeLensInfos
                    .Select(info => new CodeLensInfo
                    {
                        Line = info.Line,
                        Type = info.Type,
                        DisplayText = info.DisplayText,
                        ElementName = info.ElementName,
                        Suggestions = info.Suggestions
                    })
                    .ToList();

                DisplayCodeLens(textView, codeLensCache[fileName]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе кода для CodeLens: {ex.Message}");
            }
        }

        /// <summary>
        /// Отображает CodeLens в редакторе.
        /// </summary>
        private void DisplayCodeLens(ITextViewSnapshot textView, List<CodeLensInfo> codeLensInfos)
        {
            // Выводим информацию в отладочную консоль для демонстрации
            foreach (var info in codeLensInfos)
            {
                System.Diagnostics.Debug.WriteLine($"CodeLens на строке {info.Line}: {info.DisplayText}");
            }
        }

        /// <summary>
        /// Определяет язык по имени файла.
        /// </summary>
        private string GetLanguageFromFileName(string fileName)
        {
            if (fileName.EndsWith(".cs"))
                return "csharp";
            if (fileName.EndsWith(".vb"))
                return "vb";
            if (fileName.EndsWith(".cpp") || fileName.EndsWith(".c") || fileName.EndsWith(".h"))
                return "cpp";
            if (fileName.EndsWith(".js") || fileName.EndsWith(".ts"))
                return "javascript";
            
            return string.Empty;
        }

        /// <summary>
        /// Проверяет, поддерживается ли язык.
        /// </summary>
        private bool IsSupportedLanguage(string language)
        {
            return language == "csharp" || language == "vb" || language == "cpp" || language == "javascript";
        }
    }
}


    /// <summary>
    /// Информация для отображения в CodeLens.
    /// </summary>
    public class CodeLensInfo
    {
        public int Line { get; set; }
        public CodeLensType Type { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        
        // Для методов
        public int Complexity { get; set; }
        public int LinesOfCode { get; set; }
        public int ParameterCount { get; set; }
        
        // Для классов
        public int MethodCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
        
        // Общие свойства
        public int ReferenceCount { get; set; }
        public bool HasDocumentation { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// Типы CodeLens.
    /// </summary>
    public enum CodeLensType
    {
        Method,
        Class,
        Property,
        Field,
        Interface
    }

