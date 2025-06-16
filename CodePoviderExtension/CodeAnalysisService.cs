using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeProviderExtension
{
    /// <summary>
    /// Сервис для анализа кода с использованием Roslyn и AI.
    /// </summary>
    internal class CodeAnalysisService : ICodeAnalysisService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<CodeAnalysisService> _logger;

        public CodeAnalysisService(HttpClient httpClient, ILogger<CodeAnalysisService> logger)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<CodeAnalysisResult> AnalyzeCodeAsync(string code, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Начинаю анализ кода на языке {Language}", language);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Получен пустой код для анализа");
                    return Task.FromResult(CreateEmptyAnalysisResult(language));
                }

                var classes = new List<string>();
                var methods = new List<string>();
                var issues = new List<CodeIssue>();
                var complexityScore = 1.0;

                if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase) || 
                    language.Equals("cs", StringComparison.OrdinalIgnoreCase))
                {
                    var analysisResult = AnalyzeCSharpCode(code);
                    classes.AddRange(analysisResult.classes);
                    methods.AddRange(analysisResult.methods);
                    issues.AddRange(analysisResult.issues);
                    complexityScore = analysisResult.complexity;
                }
                else
                {
                    // Базовый анализ для других языков
                    var basicAnalysis = PerformBasicAnalysis(code, language);
                    classes.AddRange(basicAnalysis.classes);
                    methods.AddRange(basicAnalysis.methods);
                    issues.AddRange(basicAnalysis.issues);
                    complexityScore = basicAnalysis.complexity;
                }

                var result = new CodeAnalysisResult
                {
                    Language = language,
                    LineCount = code.Split('\n').Length,
                    CharacterCount = code.Length,
                    Classes = classes,
                    Methods = methods,
                    Issues = issues,
                    ComplexityScore = complexityScore
                };

                _logger.LogInformation("Анализ кода завершен успешно. Найдено: {ClassCount} классов, {MethodCount} методов", 
                    classes.Count, methods.Count);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при анализе кода на языке {Language}", language);
                return Task.FromResult(CreateEmptyAnalysisResult(language));
            }
        }

        private CodeAnalysisResult CreateEmptyAnalysisResult(string language)
        {
            return new CodeAnalysisResult
            {
                Language = language,
                LineCount = 0,
                CharacterCount = 0,
                Classes = new List<string>(),
                Methods = new List<string>(),
                Issues = new List<CodeIssue>(),
                ComplexityScore = 0.0
            };
        }

        public Task<IEnumerable<CodeSuggestion>> GetSuggestionsAsync(string code, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Генерирую предложения для кода на языке {Language}", language);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Получен пустой код для генерации предложений");
                    return Task.FromResult(Enumerable.Empty<CodeSuggestion>());
                }

                var suggestions = new List<CodeSuggestion>();

            // Проверка на типичные проблемы производительности
            if (code.Contains("StringBuilder") && code.Contains("+="))
            {
                suggestions.Add(new CodeSuggestion
                {
                    Title = "Используйте StringBuilder эффективно",
                    Description = "Обнаружено использование оператора += со StringBuilder. Используйте Append() для лучшей производительности.",
                    Type = SuggestionType.Performance,
                    StartLine = FindLineContaining(code, "StringBuilder"),
                    EndLine = FindLineContaining(code, "StringBuilder")
                });
            }

            // Проверка на магические числа
            var magicNumbers = Regex.Matches(code, @"\b\d{2,}\b").Cast<Match>()
                .Where(m => !IsInComment(code, m.Index))
                .ToList();

            foreach (var match in magicNumbers.Take(3)) // Ограничиваем количество предложений
            {
                suggestions.Add(new CodeSuggestion
                {
                    Title = "Замените магическое число на константу",
                    Description = $"Число '{match.Value}' следует вынести в именованную константу для улучшения читаемости.",
                    Type = SuggestionType.Readability,
                    StartLine = GetLineNumber(code, match.Index),
                    EndLine = GetLineNumber(code, match.Index)
                });
            }

            // Проверка на отсутствие комментариев к публичным методам
            if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
            {
                var publicMethodSuggestions = GetPublicMethodDocumentationSuggestions(code);
                suggestions.AddRange(publicMethodSuggestions);
            }

            _logger.LogInformation("Сгенерировано {Count} предложений для улучшения кода", suggestions.Count);
            return Task.FromResult<IEnumerable<CodeSuggestion>>(suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации предложений для кода на языке {Language}", language);
                return Task.FromResult(Enumerable.Empty<CodeSuggestion>());
            }
        }

        private (List<string> classes, List<string> methods, List<CodeIssue> issues, double complexity) AnalyzeCSharpCode(string code)
        {
            var classes = new List<string>();
            var methods = new List<string>();
            var issues = new List<CodeIssue>();
            var complexity = 1.0;

            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                // Поиск классов
                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classNode in classNodes)
                {
                    classes.Add(classNode.Identifier.ValueText);
                }

                // Поиск методов
                var methodNodes = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var methodNode in methodNodes)
                {
                    methods.Add(methodNode.Identifier.ValueText);
                    
                    // Анализ сложности метода
                    var methodComplexity = CalculateMethodComplexity(methodNode);
                    if (methodComplexity > 10)
                    {
                        issues.Add(new CodeIssue
                        {
                            Message = $"Метод '{methodNode.Identifier.ValueText}' имеет высокую цикломатическую сложность ({methodComplexity})",
                            Severity = IssueSeverity.Warning,
                            Line = tree.GetLineSpan(methodNode.Span).StartLinePosition.Line + 1,
                            Column = tree.GetLineSpan(methodNode.Span).StartLinePosition.Character + 1,
                            QuickFix = "Рассмотрите возможность разбития метода на более мелкие части"
                        });
                    }
                    
                    complexity += methodComplexity * 0.1;
                }

                // Поиск потенциальных проблем
                var diagnostics = tree.GetDiagnostics();
                foreach (var diagnostic in diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
                {
                    issues.Add(new CodeIssue
                    {
                        Message = diagnostic.GetMessage(),
                        Severity = ConvertSeverity(diagnostic.Severity),
                        Line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                        Column = diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1
                    });
                }
            }
            catch (Exception)
            {
                // В случае ошибки парсинга, возвращаем базовый результат
                complexity = 1.0;
            }

            return (classes, methods, issues, complexity);
        }

        private (List<string> classes, List<string> methods, List<CodeIssue> issues, double complexity) PerformBasicAnalysis(string code, string language)
        {
            var classes = new List<string>();
            var methods = new List<string>();
            var issues = new List<CodeIssue>();
            var complexity = 1.0;

            // Базовый анализ для других языков
            var lines = code.Split('\n');
            
            // Поиск классов и функций через регулярные выражения
            foreach (var (line, index) in lines.Select((l, i) => (l, i)))
            {
                if (language.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("typescript", StringComparison.OrdinalIgnoreCase))
                {
                    if (Regex.IsMatch(line, @"class\s+(\w+)"))
                    {
                        var match = Regex.Match(line, @"class\s+(\w+)");
                        classes.Add(match.Groups[1].Value);
                    }
                    
                    if (Regex.IsMatch(line, @"function\s+(\w+)") || Regex.IsMatch(line, @"(\w+)\s*=\s*function"))
                    {
                        var match = Regex.Match(line, @"function\s+(\w+)") ?? Regex.Match(line, @"(\w+)\s*=\s*function");
                        if (match.Success)
                            methods.Add(match.Groups[1].Value);
                    }
                }
                else if (language.Equals("python", StringComparison.OrdinalIgnoreCase))
                {
                    if (Regex.IsMatch(line, @"class\s+(\w+)"))
                    {
                        var match = Regex.Match(line, @"class\s+(\w+)");
                        classes.Add(match.Groups[1].Value);
                    }
                    
                    if (Regex.IsMatch(line, @"def\s+(\w+)"))
                    {
                        var match = Regex.Match(line, @"def\s+(\w+)");
                        methods.Add(match.Groups[1].Value);
                    }
                }

                // Общие проверки
                if (line.Length > 120)
                {
                    issues.Add(new CodeIssue
                    {
                        Message = "Слишком длинная строка (более 120 символов)",
                        Severity = IssueSeverity.Info,
                        Line = index + 1,
                        Column = 1,
                        QuickFix = "Разбейте строку на несколько частей"
                    });
                }
            }

            // Расчет базовой сложности
            var conditionalCount = Regex.Matches(code, @"\b(if|else|while|for|switch|case)\b").Count;
            complexity = 1.0 + conditionalCount * 0.2;

            return (classes, methods, issues, complexity);
        }

        private int CalculateMethodComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1; // базовая сложность

            // Подсчет условных операторов
            var conditionalNodes = method.DescendantNodes().Where(n =>
                n is IfStatementSyntax ||
                n is WhileStatementSyntax ||
                n is ForStatementSyntax ||
                n is ForEachStatementSyntax ||
                n is SwitchStatementSyntax ||
                n is ConditionalExpressionSyntax);

            complexity += conditionalNodes.Count();

            // Подсчет case операторов
            var caseNodes = method.DescendantNodes().OfType<SwitchSectionSyntax>();
            complexity += caseNodes.Count();

            return complexity;
        }

        private IssueSeverity ConvertSeverity(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Error => IssueSeverity.Error,
                DiagnosticSeverity.Warning => IssueSeverity.Warning,
                DiagnosticSeverity.Info => IssueSeverity.Info,
                _ => IssueSeverity.Info
            };
        }

        private int FindLineContaining(string code, string searchText)
        {
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(searchText))
                    return i + 1;
            }
            return 1;
        }

        private bool IsInComment(string code, int position)
        {
            var beforePosition = code.Substring(0, position);
            var lastNewLine = beforePosition.LastIndexOf('\n');
            var currentLine = lastNewLine >= 0 ? beforePosition.Substring(lastNewLine + 1) : beforePosition;
            
            return currentLine.TrimStart().StartsWith("//") || 
                   (beforePosition.Contains("/*") && !beforePosition.Substring(beforePosition.LastIndexOf("/*")).Contains("*/"));
        }

        private int GetLineNumber(string code, int position)
        {
            return code.Substring(0, position).Count(c => c == '\n') + 1;
        }

        private IEnumerable<CodeSuggestion> GetPublicMethodDocumentationSuggestions(string code)
        {
            var suggestions = new List<CodeSuggestion>();
            
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();
                
                var publicMethods = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

                foreach (var method in publicMethods)
                {
                    var hasDocumentation = method.HasLeadingTrivia &&
                        method.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                                          t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

                    if (!hasDocumentation)
                    {
                        suggestions.Add(new CodeSuggestion
                        {
                            Title = $"Добавьте документацию к методу '{method.Identifier.ValueText}'",
                            Description = "Публичные методы должны иметь XML-документацию для лучшего понимания API.",
                            Type = SuggestionType.BestPractice,
                            StartLine = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1,
                            EndLine = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1,
                            SuggestedCode = GenerateDocumentationTemplate(method)
                        });
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки парсинга
            }

            return suggestions;
        }

        private string GenerateDocumentationTemplate(MethodDeclarationSyntax method)
        {
            var doc = "/// <summary>\n/// \n/// </summary>\n";
            
            foreach (var parameter in method.ParameterList.Parameters)
            {
                doc += $"/// <param name=\"{parameter.Identifier.ValueText}\"></param>\n";
            }
            
            if (method.ReturnType.ToString() != "void")
            {
                doc += "/// <returns></returns>\n";
            }
            
            return doc;
        }
    }
}
