using System.Diagnostics;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace CodeProviderExtension
{
    /// <summary>
    /// Простая команда для генерации кода.
    /// </summary>
    [VisualStudioContribution]
    internal class SimpleGenerateCommand : Command
    {
        private readonly TraceSource logger;
        private readonly ICodeGenerationService codeGenerationService;

        public SimpleGenerateCommand(TraceSource traceSource, ICodeGenerationService codeGenerationService)
        {
            this.logger = Requires.NotNull(traceSource, nameof(traceSource));
            this.codeGenerationService = Requires.NotNull(codeGenerationService, nameof(codeGenerationService));
        }

        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.SimpleGenerate.DisplayName%")
        {
            Icon = new(ImageMoniker.KnownValues.NewDocument, IconSettings.IconAndText),
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        };        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Начало генерации кода");

                // Показываем простое сообщение о функциональности
                await this.Extensibility.Shell().ShowPromptAsync(
                    "Функция генерации кода готова!\n\nВ будущих версиях здесь будет возможность:\n• Генерировать код по описанию\n• Выбирать язык программирования\n• Вставлять код в редактор\n\nПока доступен только анализ кода.",
                    PromptOptions.OK,
                    cancellationToken);

                // Определяем язык (по умолчанию C#)
                var language = "csharp";

                // Получаем активное представление для определения контекста
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                if (activeTextView != null)
                {
                    var documentUri = activeTextView.Document.Uri;
                    var fileName = System.IO.Path.GetFileName(documentUri.LocalPath);
                    var fileExtension = System.IO.Path.GetExtension(fileName).TrimStart('.');
                    
                    language = fileExtension switch
                    {
                        "cs" => "csharp",
                        "js" => "javascript", 
                        "ts" => "typescript",
                        "py" => "python",
                        "java" => "java",
                        "cpp" or "cc" or "cxx" => "cpp",
                        "c" => "c",
                        _ => "csharp"
                    };

                    this.logger.TraceInformation($"Определен язык файла: {language}");
                }

                // Генерируем простой пример кода
                var generatedCode = await this.codeGenerationService.GenerateCodeAsync(
                    "Простой метод для демонстрации", 
                    language, 
                    cancellationToken);

                // Показываем сгенерированный пример
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Пример сгенерированного кода ({language}):\n\n{generatedCode}",
                    PromptOptions.OK,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при генерации кода: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка генерации: {ex.Message}",
                    PromptOptions.OK,
                    cancellationToken);
            }
        }
    }
}
