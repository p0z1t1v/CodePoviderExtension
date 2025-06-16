using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace CodeProviderExtension
{
    /// <summary>
    /// Расширенный анализатор кода для CodeLens с поддержкой различных метрик.
    /// </summary>
    public class CodeLensAnalyzer
    {
        private readonly ICodeAnalysisService codeAnalysisService;

        public CodeLensAnalyzer(ICodeAnalysisService codeAnalysisService)
        {
            this.codeAnalysisService = codeAnalysisService;
        }

        /// <summary>
        /// Анализирует код и возвращает детальную информацию для CodeLens.
        /// </summary>
        public async Task<List<DetailedCodeLensInfo>> AnalyzeCodeAsync(string sourceCode, string filePath, CancellationToken cancellationToken = default)
        {
            var results = new List<DetailedCodeLensInfo>();

            if (string.IsNullOrEmpty(sourceCode))
                return results;

            try
            {
                // Определяем язык по расширению файла
                var language = GetLanguageFromFilePath(filePath);
                
                if (language == "csharp")
                {
                    results = await AnalyzeCSharpCodeAsync(sourceCode, cancellationToken);
                }
                else if (language == "javascript" || language == "typescript")
                {
                    results = await AnalyzeJavaScriptCodeAsync(sourceCode, cancellationToken);
                }
                // Можно добавить поддержку других языков

                // Обогащаем результаты дополнительной информацией
                foreach (var result in results)
                {
                    await EnrichWithAdditionalInfoAsync(result, sourceCode, cancellationToken);
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
        private async Task<List<DetailedCodeLensInfo>> AnalyzeCSharpCodeAsync(string sourceCode, CancellationToken cancellationToken)
        {
            var results = new List<DetailedCodeLensInfo>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = await syntaxTree.GetRootAsync(cancellationToken);

                // Анализируем классы
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                {
                    var classInfo = await AnalyzeClassDetailedAsync(@class, sourceCode, cancellationToken);
                    if (classInfo != null)
                        results.Add(classInfo);
                }

                // Анализируем методы
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    var methodInfo = await AnalyzeMethodDetailedAsync(method, sourceCode, cancellationToken);
                    if (methodInfo != null)
                        results.Add(methodInfo);
                }

                // Анализируем свойства
                var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach (var property in properties)
                {
                    var propertyInfo = await AnalyzePropertyDetailedAsync(property, sourceCode, cancellationToken);
                    if (propertyInfo != null)
                        results.Add(propertyInfo);
                }

                // Анализируем интерфейсы
                var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
                foreach (var @interface in interfaces)
                {
                    var interfaceInfo = await AnalyzeInterfaceDetailedAsync(@interface, sourceCode, cancellationToken);
                    if (interfaceInfo != null)
                        results.Add(interfaceInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе C# кода: {ex.Message}");
            }

            return results;
        }        /// <summary>
        /// Детальный анализ класса.
        /// </summary>
        private Task<DetailedCodeLensInfo?> AnalyzeClassDetailedAsync(ClassDeclarationSyntax @class, string sourceCode, CancellationToken cancellationToken)
        {
            try
            {
                var lineSpan = @class.SyntaxTree.GetLineSpan(@class.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var methods = @class.Members.OfType<MethodDeclarationSyntax>().ToList();
                var properties = @class.Members.OfType<PropertyDeclarationSyntax>().ToList();
                var fields = @class.Members.OfType<FieldDeclarationSyntax>().ToList();
                var constructors = @class.Members.OfType<ConstructorDeclarationSyntax>().ToList();

                // Анализируем наследование
                var baseTypes = @class.BaseList?.Types.Select(t => t.Type.ToString()).ToList() ?? new List<string>();
                var isAbstract = @class.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
                var isSealed = @class.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword));
                var isStatic = @class.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

                // Вычисляем метрики класса
                var totalLinesOfCode = GetLinesOfCode(@class);
                var publicMethods = methods.Count(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));
                var privateMethods = methods.Count(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PrivateKeyword)));

                // Анализируем сложность
                var averageComplexity = methods.Any() 
                    ? methods.Average(m => CalculateCyclomaticComplexity(m))
                    : 0;

                var info = new DetailedCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Class,
                    ElementName = @class.Identifier.ValueText,
                    Metrics = new CodeMetrics
                    {
                        LinesOfCode = totalLinesOfCode,
                        CyclomaticComplexity = (int)Math.Round(averageComplexity),
                        MethodCount = methods.Count,
                        PropertyCount = properties.Count,
                        FieldCount = fields.Count,
                        ConstructorCount = constructors.Count,
                        PublicMethods = publicMethods,
                        PrivateMethods = privateMethods
                    },
                    ClassDetails = new ClassDetails
                    {
                        BaseTypes = baseTypes,
                        IsAbstract = isAbstract,
                        IsSealed = isSealed,
                        IsStatic = isStatic,
                        HasDocumentation = HasXmlDocumentation(@class)
                    }
                };                return Task.FromResult<DetailedCodeLensInfo?>(info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при детальном анализе класса: {ex.Message}");
                return Task.FromResult<DetailedCodeLensInfo?>(null);
            }
        }        /// <summary>
        /// Детальный анализ метода.
        /// </summary>
        private Task<DetailedCodeLensInfo?> AnalyzeMethodDetailedAsync(MethodDeclarationSyntax method, string sourceCode, CancellationToken cancellationToken)
        {
            try
            {
                var lineSpan = method.SyntaxTree.GetLineSpan(method.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var complexity = CalculateCyclomaticComplexity(method);
                var linesOfCode = GetLinesOfCode(method);
                var parameterCount = method.ParameterList.Parameters.Count;
                
                // Анализируем модификаторы
                var isPublic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                var isPrivate = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));
                var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                var isAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
                var isVirtual = method.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword));
                var isOverride = method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword));

                // Анализируем возвращаемый тип
                var returnType = method.ReturnType.ToString();
                var hasReturnValue = returnType != "void";

                // Подсчитываем операторы и выражения
                var statements = method.Body?.Statements.Count ?? 0;
                var expressions = method.DescendantNodes().OfType<ExpressionSyntax>().Count();
                
                // Анализируем зависимости
                var methodCalls = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(inv => inv.Expression.ToString())
                    .Distinct()
                    .ToList();

                var info = new DetailedCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Method,
                    ElementName = method.Identifier.ValueText,
                    Metrics = new CodeMetrics
                    {
                        LinesOfCode = linesOfCode,
                        CyclomaticComplexity = complexity,
                        ParameterCount = parameterCount,
                        StatementCount = statements,
                        ExpressionCount = expressions
                    },
                    MethodDetails = new MethodDetails
                    {
                        ReturnType = returnType,
                        HasReturnValue = hasReturnValue,
                        IsPublic = isPublic,
                        IsPrivate = isPrivate,
                        IsStatic = isStatic,
                        IsAsync = isAsync,
                        IsVirtual = isVirtual,
                        IsOverride = isOverride,
                        MethodCalls = methodCalls,
                        HasDocumentation = HasXmlDocumentation(method)
                    }
                };

                return Task.FromResult<DetailedCodeLensInfo?>(info);
            }            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при детальном анализе метода: {ex.Message}");
                return Task.FromResult<DetailedCodeLensInfo?>(null);
            }
        }        /// <summary>
        /// Детальный анализ свойства.
        /// </summary>
        private async Task<DetailedCodeLensInfo?> AnalyzePropertyDetailedAsync(PropertyDeclarationSyntax property, string sourceCode, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield(); // Делаем метод по-настоящему асинхронным
                
                var lineSpan = property.SyntaxTree.GetLineSpan(property.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var hasGetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false;
                var hasSetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false;
                var isAutoProperty = property.AccessorList?.Accessors.All(a => a.Body == null && a.ExpressionBody == null) ?? false;

                var info = new DetailedCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Property,
                    ElementName = property.Identifier.ValueText,
                    PropertyDetails = new PropertyDetails
                    {
                        PropertyType = property.Type.ToString(),
                        HasGetter = hasGetter,
                        HasSetter = hasSetter,
                        IsAutoProperty = isAutoProperty,
                        HasDocumentation = HasXmlDocumentation(property)
                    }
                };

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе свойства: {ex.Message}");
                return null;
            }
        }        /// <summary>
        /// Детальный анализ интерфейса.
        /// </summary>
        private async Task<DetailedCodeLensInfo?> AnalyzeInterfaceDetailedAsync(InterfaceDeclarationSyntax @interface, string sourceCode, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield(); // Делаем метод по-настоящему асинхронным
                
                var lineSpan = @interface.SyntaxTree.GetLineSpan(@interface.Span);
                var startLine = lineSpan.StartLinePosition.Line;

                var methods = @interface.Members.OfType<MethodDeclarationSyntax>().Count();
                var properties = @interface.Members.OfType<PropertyDeclarationSyntax>().Count();
                var events = @interface.Members.OfType<EventDeclarationSyntax>().Count();

                var info = new DetailedCodeLensInfo
                {
                    Line = startLine,
                    Type = CodeLensType.Interface,
                    ElementName = @interface.Identifier.ValueText,
                    Metrics = new CodeMetrics
                    {
                        MethodCount = methods,
                        PropertyCount = properties,
                        EventCount = events
                    },
                    InterfaceDetails = new InterfaceDetails
                    {
                        MemberCount = methods + properties + events,
                        HasDocumentation = HasXmlDocumentation(@interface)
                    }
                };

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при анализе интерфейса: {ex.Message}");
                return null;
            }
        }        /// <summary>
        /// Анализирует JavaScript/TypeScript код (базовая реализация).
        /// </summary>
        private async Task<List<DetailedCodeLensInfo>> AnalyzeJavaScriptCodeAsync(string sourceCode, CancellationToken cancellationToken)
        {
            await Task.Yield(); // Делаем метод по-настоящему асинхронным
            
            var results = new List<DetailedCodeLensInfo>();
            
            // Простой анализ JavaScript - поиск функций
            var lines = sourceCode.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Поиск функций
                if (line.StartsWith("function ") || line.Contains("= function") || line.Contains("=>"))
                {
                    var functionName = ExtractJavaScriptFunctionName(line);
                    if (!string.IsNullOrEmpty(functionName))
                    {
                        var info = new DetailedCodeLensInfo
                        {
                            Line = i,
                            Type = CodeLensType.Method,
                            ElementName = functionName,
                            Metrics = new CodeMetrics
                            {
                                LinesOfCode = CountJavaScriptFunctionLines(lines, i)
                            },
                            MethodDetails = new MethodDetails
                            {
                                HasDocumentation = i > 0 && lines[i - 1].Trim().StartsWith("/**")
                            }
                        };
                        
                        results.Add(info);
                    }
                }
            }
            
            return results;
        }

        /// <summary>
        /// Обогащает результаты анализа дополнительной информацией.
        /// </summary> 
               private async Task EnrichWithAdditionalInfoAsync(DetailedCodeLensInfo info, string sourceCode, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield(); // Делаем метод по-настоящему асинхронным
                
                // Добавляем предложения по улучшению на основе анализа
                info.Suggestions = GenerateSuggestions(info);

                // Вычисляем приоритет отображения
                info.Priority = CalculateDisplayPriority(info);

                // Генерируем текст для отображения
                info.DisplayText = GenerateDisplayText(info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обогащении информации CodeLens: {ex.Message}");
            }
        }

        /// <summary>
        /// Вычисляет цикломатическую сложность метода.
        /// </summary>
        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1; // Базовая сложность

            if (method.Body == null) return complexity;

            var conditionalNodes = method.Body.DescendantNodes().Where(node =>
                node is IfStatementSyntax ||
                node is WhileStatementSyntax ||
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax ||
                node is SwitchStatementSyntax ||
                node is ConditionalExpressionSyntax ||
                node is CatchClauseSyntax ||
                node is BinaryExpressionSyntax binaryExpr && 
                    (binaryExpr.IsKind(SyntaxKind.LogicalAndExpression) || 
                     binaryExpr.IsKind(SyntaxKind.LogicalOrExpression)));

            complexity += conditionalNodes.Count();

            return complexity;
        }

        /// <summary>
        /// Подсчитывает строки кода (исключая пустые строки и комментарии).
        /// </summary>
        private int GetLinesOfCode(SyntaxNode node)
        {
            var text = node.ToString();
            var lines = text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line.Trim()) && 
                              !line.Trim().StartsWith("//") && 
                              !line.Trim().StartsWith("/*") &&
                              !line.Trim().StartsWith("*"))
                .Count();
            
            return lines;
        }

        /// <summary>
        /// Проверяет наличие XML документации.
        /// </summary>
     bool HasXmlDocumentation(SyntaxNode node)
        {
            var trivias = node.GetLeadingTrivia();
            return trivias.Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                   t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }

        /// <summary>
        /// Определяет язык программирования по пути к файлу.
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
                ".cpp" or ".cc" or ".cxx" => "cpp",
                ".c" => "c",
                ".h" or ".hpp" => "cpp",
                ".py" => "python",
                ".java" => "java",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Извлекает имя функции из строки JavaScript.
        /// </summary>
        private string ExtractJavaScriptFunctionName(string line)
        {
            try
            {
                if (line.StartsWith("function "))
                {
                    var parts = line.Split('(');
                    if (parts.Length > 0)
                    {
                        return parts[0].Replace("function ", "").Trim();
                    }
                }
                else if (line.Contains("= function"))
                {
                    var parts = line.Split('=');
                    if (parts.Length > 0)
                    {
                        return parts[0].Trim();
                    }
                }
                else if (line.Contains("=>"))
                {
                    var parts = line.Split('=');
                    if (parts.Length > 0)
                    {
                        return parts[0].Trim();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки парсинга
            }

            return string.Empty;
        }

        /// <summary>
        /// Подсчитывает строки JavaScript функции.
        /// </summary>
        private int CountJavaScriptFunctionLines(string[] lines, int startIndex)
        {
            var count = 1;
            var braceCount = 0;
            
            for (int i = startIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                braceCount += line.Count(c => c == '{') - line.Count(c => c == '}');
                
                if (i > startIndex && braceCount <= 0)
                    break;
                    
                count++;
            }
            
            return count;
        }

        /// <summary>
        /// Генерирует предложения по улучшению кода.
        /// </summary>
        private List<string> GenerateSuggestions(DetailedCodeLensInfo info)
        {
            var suggestions = new List<string>();
            var settings = CodeLensSettings.Instance;

            if (info.Metrics != null)
            {
                // Предложения по сложности
                if (info.Metrics.CyclomaticComplexity > settings.ComplexityWarningThreshold)
                {
                    suggestions.Add($"Высокая сложность ({info.Metrics.CyclomaticComplexity}). Рассмотрите рефакторинг.");
                }

                // Предложения по длине метода
                if (info.Metrics.LinesOfCode > settings.MaxMethodLines)
                {
                    suggestions.Add($"Длинный метод ({info.Metrics.LinesOfCode} строк). Рассмотрите разбиение.");
                }

                // Предложения по параметрам
                if (info.Metrics.ParameterCount > 5)
                {
                    suggestions.Add($"Много параметров ({info.Metrics.ParameterCount}). Рассмотрите объект параметров.");
                }
            }

            // Предложения по документации
            if ((info.MethodDetails?.HasDocumentation == false || 
                 info.ClassDetails?.HasDocumentation == false) && 
                info.Type != CodeLensType.Property)
            {
                suggestions.Add("Отсутствует документация. Добавьте XML комментарии.");
            }

            return suggestions;
        }

        /// <summary>
        /// Вычисляет приоритет отображения для элемента.
        /// </summary>
        private int CalculateDisplayPriority(DetailedCodeLensInfo info)
        {
            var priority = 0;

            // Высокий приоритет для сложных методов
            if (info.Metrics?.CyclomaticComplexity > 10)
                priority += 10;

            // Высокий приоритет для длинных методов
            if (info.Metrics?.LinesOfCode > 50)
                priority += 5;

            // Высокий приоритет для публичных элементов
            if (info.MethodDetails?.IsPublic == true)
                priority += 3;

            // Высокий приоритет для элементов с предложениями
            priority += info.Suggestions.Count;

            return priority;
        }

        /// <summary>
        /// Генерирует текст для отображения в CodeLens.
        /// </summary>
         string GenerateDisplayText(DetailedCodeLensInfo info)
        {
            var parts = new List<string>();
            var settings = CodeLensSettings.Instance;

            if (info.Type == CodeLensType.Method && info.Metrics != null)
            {
                if (settings.ShowComplexity)
                    parts.Add($"Сложность: {info.Metrics.CyclomaticComplexity}");
                
                if (settings.ShowLinesOfCode)
                    parts.Add($"Строк: {info.Metrics.LinesOfCode}");
                
                if (settings.ShowParameterCount && info.Metrics.ParameterCount > 0)
                    parts.Add($"Параметров: {info.Metrics.ParameterCount}");
            }
            else if (info.Type == CodeLensType.Class && info.Metrics != null)
            {
                if (settings.ShowClassInfo)
                {
                    var classParts = new List<string>();
                    if (info.Metrics.MethodCount > 0)
                        classParts.Add($"Методов: {info.Metrics.MethodCount}");
                    if (info.Metrics.PropertyCount > 0)
                        classParts.Add($"Свойств: {info.Metrics.PropertyCount}");
                    
                    parts.AddRange(classParts);
                }
            }

            // Добавляем предупреждения
            if (info.Suggestions.Any())
            {
                var warningCount = info.Suggestions.Count;
                parts.Add($"⚠ {warningCount} предложений");
            }

            return parts.Any() ? string.Join(" | ", parts) : info.ElementName;
        }

    
    }

    /// <summary>
    /// Детальная информация о коде для CodeLens.
    /// </summary>
    public class DetailedCodeLensInfo
    {
        public int Line { get; set; }
        public CodeLensType Type { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> Suggestions { get; set; } = new();
        
        public CodeMetrics? Metrics { get; set; }
        public MethodDetails? MethodDetails { get; set; }
        public ClassDetails? ClassDetails { get; set; }
        public PropertyDetails? PropertyDetails { get; set; }
        public InterfaceDetails? InterfaceDetails { get; set; }
    }

    /// <summary>
    /// Метрики кода.
    /// </summary>
    public class CodeMetrics
    {
        public int LinesOfCode { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int ParameterCount { get; set; }
        public int MethodCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
        public int ConstructorCount { get; set; }
        public int PublicMethods { get; set; }
        public int PrivateMethods { get; set; }
        public int StatementCount { get; set; }
        public int ExpressionCount { get; set; }
        public int EventCount { get; set; }
    }

    /// <summary>
    /// Детали метода.
    /// </summary>
    public class MethodDetails
    {
        public string ReturnType { get; set; } = string.Empty;
        public bool HasReturnValue { get; set; }
        public bool IsPublic { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsStatic { get; set; }
        public bool IsAsync { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsOverride { get; set; }
        public bool HasDocumentation { get; set; }
        public List<string> MethodCalls { get; set; } = new();
    }

    /// <summary>
    /// Детали класса.
    /// </summary>
    public class ClassDetails
    {
        public List<string> BaseTypes { get; set; } = new();
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }
        public bool HasDocumentation { get; set; }
    }

    /// <summary>
    /// Детали свойства.
    /// </summary>
    public class PropertyDetails
    {
        public string PropertyType { get; set; } = string.Empty;
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public bool IsAutoProperty { get; set; }
        public bool HasDocumentation { get; set; }
    }

    /// <summary>
    /// Детали интерфейса.
    /// </summary>
    public class InterfaceDetails
    {
        public int MemberCount { get; set; }
        public bool HasDocumentation { get; set; }
    }
}
