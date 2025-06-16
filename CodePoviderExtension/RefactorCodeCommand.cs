using System.Diagnostics;
using System.Text;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для рефакторинга выделенного кода.
    /// </summary>
    [VisualStudioContribution]
    internal class RefactorCodeCommand : Command
    {
        private readonly TraceSource logger;
        private readonly ICodeGenerationService codeGenerationService;        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.RefactorCode.DisplayName%")
        {
            TooltipText = "%CodeProviderExtension.RefactorCode.TooltipText%",
            Icon = null,
            EnabledWhen = null,
            VisibleWhen = null
        };

        public RefactorCodeCommand(TraceSource traceSource, ICodeGenerationService codeGenerationService)
        {
            this.logger = Requires.NotNull(traceSource, nameof(traceSource));
            this.codeGenerationService = Requires.NotNull(codeGenerationService, nameof(codeGenerationService));
        }

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Начало рефакторинга кода");

                // Получаем активное представление текста
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                
                if (activeTextView == null)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет активного документа для рефакторинга!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }

                // Проверяем выделенный текст
                var selection = activeTextView.Selection;
                if (selection.IsEmpty)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Выделите код для рефакторинга!",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }

                // Получаем выделенный код
                var selectedCode = selection.Extent.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(selectedCode))
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Выделенный текст пуст!", 
                        PromptOptions.OK, 
                        cancellationToken);
                    return;
                }                // Применяем автоматический рефакторинг с улучшением читаемости
                var instructions = "Улучшить читаемость и применить best practices";
                this.logger.TraceInformation($"Рефакторинг кода с инструкциями: {instructions}");

                // Выполняем рефакторинг
                var refactoredCode = await this.codeGenerationService.RefactorCodeAsync(
                    selectedCode, 
                    instructions, 
                    cancellationToken);                // Показываем результат пользователю
                var resultDialog = BuildPreviewDialog(selectedCode, refactoredCode, instructions);
                await this.Extensibility.Shell().ShowPromptAsync(
                    resultDialog,
                    PromptOptions.OK,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при рефакторинге: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"❌ Ошибка рефакторинга: {ex.Message}", 
                    PromptOptions.OK, 
                    cancellationToken);
            }
        }

        private static string[] GetRefactoringOptions()
        {
            return new[]
            {
                "Улучшить производительность",
                "Повысить читаемость",
                "Добавить обработку ошибок",
                "Упростить код",
                "Добавить комментарии",
                "Применить best practices"
            };
        }

        private static string ParseRefactoringInstructions(string userInput, string[] options)
        {
            // Проверяем, ввёл ли пользователь номер опции
            if (int.TryParse(userInput.Trim(), out var optionNumber) && 
                optionNumber >= 1 && optionNumber <= options.Length)
            {
                return options[optionNumber - 1];
            }

            // Иначе используем прямой ввод пользователя
            return userInput;
        }

        private static string BuildPreviewDialog(string originalCode, string refactoredCode, string instructions)
        {
            var dialog = new StringBuilder();
            
            dialog.AppendLine("🔍 ПРЕДВАРИТЕЛЬНЫЙ ПРОСМОТР РЕФАКТОРИНГА");
            dialog.AppendLine(new string('═', 60));
            dialog.AppendLine($"📝 Применённые изменения: {instructions}");
            dialog.AppendLine();
            
            dialog.AppendLine("📋 ИСХОДНЫЙ КОД:");
            dialog.AppendLine(new string('─', 30));
            var originalLines = originalCode.Split('\n');
            for (int i = 0; i < Math.Min(originalLines.Length, 10); i++)
            {
                dialog.AppendLine($"  {i + 1:D2} | {originalLines[i]}");
            }
            if (originalLines.Length > 10)
            {
                dialog.AppendLine($"  ... ещё {originalLines.Length - 10} строк");
            }
            dialog.AppendLine();
            
            dialog.AppendLine("✨ РЕФАКТОРИРОВАННЫЙ КОД:");
            dialog.AppendLine(new string('─', 30));
            var refactoredLines = refactoredCode.Split('\n');
            for (int i = 0; i < Math.Min(refactoredLines.Length, 10); i++)
            {
                dialog.AppendLine($"  {i + 1:D2} | {refactoredLines[i]}");
            }
            if (refactoredLines.Length > 10)
            {
                dialog.AppendLine($"  ... ещё {refactoredLines.Length - 10} строк");
            }
            dialog.AppendLine();
            
            dialog.AppendLine("❓ Применить рефакторинг?");
            
            return dialog.ToString();
        }
    }
}
