using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace CodeProviderExtension
{    /// <summary>
    /// Упрощенный анализатор кода для CodeLens.
    /// </summary>
    public class SimpleCodeLensAnalyzer
    {
        /// <summary>
        /// Анализирует код и возвращает информацию для CodeLens.
        /// </summary>
        public List<SimpleCodeLensInfo> AnalyzeCode(string sourceCode, string filePath)
        {
            var results = new List<SimpleCodeLensInfo>();

            if (string.IsNullOrEmpty(sourceCode))
                return results;

            try
            {
                var language = GetLanguageFromFilePath(filePath);
                
                if (language == "csharp")
                {
                    results = AnalyzeCSharpCode(sourceCode);
                }

                // Обогащаем результаты
                foreach (var result in results)
                {
                    EnrichWithAdditionalInfo(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе кода для CodeLens: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Анализирует C# код.
        /// </summary>
        private List<SimpleCodeLensInfo> AnalyzeCSharpCode(string sourceCode)
        {
            var results = new List<SimpleCodeLensInfo>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Анализируем классы
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                {
                    var classInfo = AnalyzeClass(@class);
                    if (classInfo != null)
                        results.Add(classInfo);
                }

                // Анализируем методы
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    var methodInfo = AnalyzeMethod(method);
                    if (methodInfo != null)
                        results.Add(methodInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе C# кода: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Анализирует класс.
        /// </summary>
        private SimpleCodeLensInfo? AnalyzeClass(ClassDeclarationSyntax @class)
        {
            try
            {
                var lineSpan = @class.SyntaxTree.GetLineSpan(@class.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var methods = @class.Members.OfType<MethodDeclarationSyntax>().Count();
                var properties = @class.Members.OfType<PropertyDeclarationSyntax>().Count();
                var fields = @class.Members.OfType<FieldDeclarationSyntax>().Count();

                return new SimpleCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Class,
                    ElementName = @class.Identifier.ValueText,
                    LinesOfCode = GetLinesOfCode(@class),
                    MethodCount = methods,
                    PropertyCount = properties,
                    FieldCount = fields
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе класса: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Анализирует метод.
        /// </summary>
        private SimpleCodeLensInfo? AnalyzeMethod(MethodDeclarationSyntax method)
        {
            try
            {
                var lineSpan = method.SyntaxTree.GetLineSpan(method.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var complexity = CalculateCyclomaticComplexity(method);
                var linesOfCode = GetLinesOfCode(method);
                var parameterCount = method.ParameterList.Parameters.Count;

                return new SimpleCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Method,
                    ElementName = method.Identifier.ValueText,
                    LinesOfCode = linesOfCode,
                    CyclomaticComplexity = complexity,
                    ParameterCount = parameterCount
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе метода: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Обогащает информацию дополнительными данными.
        /// </summary>
        private void EnrichWithAdditionalInfo(SimpleCodeLensInfo info)
        {
            try
            {
                info.Suggestions = GenerateSuggestions(info);
                info.Priority = CalculateDisplayPriority(info);
                info.DisplayText = GenerateDisplayText(info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обогащении информации: {ex.Message}");
            }
        }

        #region Вспомогательные методы

        /// <summary>
        /// Вычисляет цикломатическую сложность метода.
        /// </summary>
        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1;

            if (method.Body == null) return complexity;

            var conditionalNodes = method.Body.DescendantNodes().Where(node =>
                node is IfStatementSyntax ||
                node is WhileStatementSyntax ||
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax ||
                node is SwitchStatementSyntax ||
                node is ConditionalExpressionSyntax ||
                node is CatchClauseSyntax);

            complexity += conditionalNodes.Count();

            return complexity;
        }

        /// <summary>
        /// Подсчитывает строки кода.
        /// </summary>
        private int GetLinesOfCode(SyntaxNode node)
        {
            var text = node.ToString();
            var lines = text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line.Trim()) && 
                              !line.Trim().StartsWith("//") && 
                              !line.Trim().StartsWith("/*"))
                .Count();
            
            return lines;
        }

        /// <summary>
        /// Определяет язык по расширению файла.
        /// </summary>
        private string GetLanguageFromFilePath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "csharp",
                ".vb" => "vb",
                ".js" => "javascript",
                ".ts" => "typescript",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Генерирует предложения по улучшению.
        /// </summary>
        private List<string> GenerateSuggestions(SimpleCodeLensInfo info)
        {
            var suggestions = new List<string>();
            var settings = CodeLensSettings.Instance;

            if (info.CyclomaticComplexity > settings.ComplexityWarningThreshold)
            {
                suggestions.Add($"Высокая сложность ({info.CyclomaticComplexity})");
            }

            if (info.LinesOfCode > settings.MaxMethodLines)
            {
                suggestions.Add($"Длинный метод ({info.LinesOfCode} строк)");
            }

            if (info.ParameterCount > 5)
            {
                suggestions.Add($"Много параметров ({info.ParameterCount})");
            }

            return suggestions;
        }

        /// <summary>
        /// Вычисляет приоритет отображения.
        /// </summary>
        private int CalculateDisplayPriority(SimpleCodeLensInfo info)
        {
            var priority = 0;

            if (info.CyclomaticComplexity > 10)
                priority += 10;

            if (info.LinesOfCode > 50)
                priority += 5;

            priority += info.Suggestions.Count;

            return priority;
        }

        /// <summary>
        /// Генерирует текст для отображения.
        /// </summary>
        private string GenerateDisplayText(SimpleCodeLensInfo info)
        {
            var parts = new List<string>();
            var settings = CodeLensSettings.Instance;

            if (info.Type == CodeLensType.Method)
            {
                if (settings.ShowComplexity)
                    parts.Add($"Сложность: {info.CyclomaticComplexity}");
                
                if (settings.ShowLinesOfCode)
                    parts.Add($"Строк: {info.LinesOfCode}");
                
                if (settings.ShowParameterCount && info.ParameterCount > 0)
                    parts.Add($"Параметров: {info.ParameterCount}");
            }
            else if (info.Type == CodeLensType.Class)
            {
                if (settings.ShowClassInfo)
                {
                    var classParts = new List<string>();
                    if (info.MethodCount > 0)
                        classParts.Add($"Методов: {info.MethodCount}");
                    if (info.PropertyCount > 0)
                        classParts.Add($"Свойств: {info.PropertyCount}");
                    
                    parts.AddRange(classParts);
                }
            }

            if (info.Suggestions.Any())
            {
                parts.Add($"⚠ {info.Suggestions.Count} предложений");
            }

            return parts.Any() ? string.Join(" | ", parts) : info.ElementName;
        }

        #endregion
    }

    /// <summary>
    /// Упрощенная информация о коде для CodeLens.
    /// </summary>
    public class SimpleCodeLensInfo
    {
        public int Line { get; set; }
        public CodeLensType Type { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> Suggestions { get; set; } = new();
        
        // Метрики
        public int LinesOfCode { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int ParameterCount { get; set; }
        public int MethodCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
    }
}
