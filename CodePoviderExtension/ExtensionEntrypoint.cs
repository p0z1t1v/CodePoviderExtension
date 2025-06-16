using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace CodeProviderExtension
{
    /// <summary>
    /// Точка входа расширения для генерации и анализа кода с использованием VisualStudio.Extensibility.
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        /// <inheritdoc/>
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            Metadata = new(
                    id: "CodeProviderExtension.4424dff2-f259-4d92-ac1f-a1853a5d2845",
                    version: this.ExtensionAssemblyVersion,
                    publisherName: "CodeProvider Team",
                    displayName: "Code Provider Extension",
                    description: "Мощное расширение для анализа, генерации и рефакторинга кода с поддержкой искусственного интеллекта"),
        };

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // Регистрируем сервисы для работы с кодом
            serviceCollection.AddSingleton<ICodeAnalysisService, CodeAnalysisService>();
            serviceCollection.AddSingleton<ICodeGenerationService, CodeGenerationService>();
            
            // Добавляем HTTP клиент для внешних API
            serviceCollection.AddHttpClient();
            
            // Настраиваем логирование
            serviceCollection.AddLogging();
        }
    }

    /// <summary>
    /// Интерфейс для сервиса анализа кода.
    /// </summary>
    public interface ICodeAnalysisService
    {
        Task<CodeAnalysisResult> AnalyzeCodeAsync(string code, string language, CancellationToken cancellationToken = default);
        Task<IEnumerable<CodeSuggestion>> GetSuggestionsAsync(string code, string language, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Интерфейс для сервиса генерации кода.
    /// </summary>
    public interface ICodeGenerationService
    {
        Task<string> GenerateCodeAsync(string prompt, string language, CancellationToken cancellationToken = default);
        Task<string> RefactorCodeAsync(string code, string instructions, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Интерфейс для сервиса документирования.
    /// </summary>
    public interface IDocumentationService
    {
        Task<string> GenerateDocumentationAsync(string code, string language, CancellationToken cancellationToken = default);
        Task<string> ExplainCodeAsync(string code, string language, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Результат анализа кода.
    /// </summary>
    public class CodeAnalysisResult
    {
        public required string Language { get; init; }
        public required int LineCount { get; init; }
        public required int CharacterCount { get; init; }
        public required IEnumerable<string> Classes { get; init; }
        public required IEnumerable<string> Methods { get; init; }
        public required IEnumerable<CodeIssue> Issues { get; init; }
        public required double ComplexityScore { get; init; }
    }

    /// <summary>
    /// Предложение по улучшению кода.
    /// </summary>
    public class CodeSuggestion
    {
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required SuggestionType Type { get; init; }
        public required int StartLine { get; init; }
        public required int EndLine { get; init; }
        public string? SuggestedCode { get; init; }
    }

    /// <summary>
    /// Проблема в коде.
    /// </summary>
    public class CodeIssue
    {
        public required string Message { get; init; }
        public required IssueSeverity Severity { get; init; }
        public required int Line { get; init; }
        public required int Column { get; init; }
        public string? QuickFix { get; init; }
    }

    /// <summary>
    /// Типы предложений.
    /// </summary>
    public enum SuggestionType
    {
        Performance,
        Readability,
        BestPractice,
        Security,
        Refactoring
    }

    /// <summary>
    /// Уровни серьезности проблем.
    /// </summary>
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}
