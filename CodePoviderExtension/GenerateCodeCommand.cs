using System.Diagnostics;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для генерации кода на основе пользовательского запроса.
    /// </summary>
    [VisualStudioContribution]
    internal class GenerateCodeCommand : Command
    {
        private readonly TraceSource logger;
        private readonly ICodeGenerationService codeGenerationService;        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.GenerateCode.DisplayName%")
        {
            TooltipText = "%CodeProviderExtension.GenerateCode.TooltipText%",
            Icon = null,
            EnabledWhen = null,
            VisibleWhen = null
        };

        public GenerateCodeCommand(TraceSource traceSource, ICodeGenerationService codeGenerationService)
        {
            this.logger = Requires.NotNull(traceSource, nameof(traceSource));
            this.codeGenerationService = Requires.NotNull(codeGenerationService, nameof(codeGenerationService));
        }

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Начало генерации кода");

                // Получаем активное представление текста
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                
                if (activeTextView == null)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет активного документа для вставки кода!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }

                // Получаем информацию о документе для определения языка
                var documentUri = activeTextView.Document.Uri;
                var fileName = System.IO.Path.GetFileName(documentUri.LocalPath);
                var fileExtension = System.IO.Path.GetExtension(fileName).TrimStart('.');                // Генерируем базовый шаблон кода для текущего языка
                var userPrompt = "Базовый шаблон класса";
                this.logger.TraceInformation($"Генерация кода для запроса: {userPrompt}");

                // Генерируем код
                var generatedCode = await this.codeGenerationService.GenerateCodeAsync(
                    userPrompt, 
                    fileExtension, 
                    cancellationToken);

                // Показываем результат пользователю
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"✅ Код успешно сгенерирован!\n\nОписание: {userPrompt}\nСгенерировано строк: {generatedCode.Split('\n').Length}\n\n📋 Сгенерированный код:\n{generatedCode.Substring(0, Math.Min(generatedCode.Length, 500))}...",
                    PromptOptions.OK,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при генерации кода: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"❌ Ошибка генерации кода: {ex.Message}", 
                    PromptOptions.OK, 
                    cancellationToken);
            }
        }
    }
}
