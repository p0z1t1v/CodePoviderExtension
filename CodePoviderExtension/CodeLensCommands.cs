using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using System.Diagnostics;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для переключения отображения CodeLens.
    /// </summary>
    [VisualStudioContribution]
    internal class ToggleCodeLensCommand : Command
    {
        private static bool isCodeLensEnabled = true;
        private readonly TraceSource logger;

        /// <summary>
        /// Конфигурация команды.
        /// </summary>
        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.ToggleCodeLens.DisplayName%")
        {
            TooltipText = "%CodeProviderExtension.ToggleCodeLens.TooltipText%",
            Icon = null,
            Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
        };

        /// <summary>
        /// Конструктор команды.
        /// </summary>
        public ToggleCodeLensCommand(VisualStudioExtensibility extensibility) : base(extensibility)
        {
            this.logger = new TraceSource("ToggleCodeLensCommand");
        }

        /// <summary>
        /// Выполнение команды переключения CodeLens.
        /// </summary>
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                isCodeLensEnabled = !isCodeLensEnabled;
                
                var status = isCodeLensEnabled ? "включены" : "отключены";
                var message = $"CodeLens {status}";
                
                this.logger.TraceInformation($"CodeLens переключен: {status}");

                // Показываем уведомление пользователю
                await this.Extensibility.Shell().ShowPromptAsync(
                    message,
                    PromptOptions.OK,
                    cancellationToken);

                // Здесь можно добавить логику для фактического включения/отключения CodeLens
                // Например, уведомить CodeLensProvider о изменении состояния
            }            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при переключении CodeLens: {ex.Message}");
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка при переключении CodeLens: {ex.Message}",
                    PromptOptions.OK,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Получает текущее состояние CodeLens.
        /// </summary>
        public static bool IsCodeLensEnabled => isCodeLensEnabled;
    }

    /// <summary>
    /// Команда для настроек CodeLens.
    /// </summary>
    [VisualStudioContribution]
    internal class CodeLensSettingsCommand : Command
    {
        private readonly TraceSource logger;

        /// <summary>
        /// Конфигурация команды.
        /// </summary>
        public override CommandConfiguration CommandConfiguration => new("%CodeProviderExtension.CodeLensSettings.DisplayName%")
        {
            TooltipText = "%CodeProviderExtension.CodeLensSettings.TooltipText%",
            Icon = null,
            Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
        };

        /// <summary>
        /// Конструктор команды.
        /// </summary>
        public CodeLensSettingsCommand(VisualStudioExtensibility extensibility) : base(extensibility)
        {
            this.logger = new TraceSource("CodeLensSettingsCommand");
        }

        /// <summary>
        /// Выполнение команды настроек CodeLens.
        /// </summary>
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.TraceInformation("Открытие настроек CodeLens");

                // Создаем диалог настроек
                var settingsDialog = new CodeLensSettingsDialog(this.Extensibility);
                await settingsDialog.ShowAsync(cancellationToken);
            }            catch (Exception ex)
            {
                this.logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при открытии настроек CodeLens: {ex.Message}");
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка при открытии настроек: {ex.Message}",
                    PromptOptions.OK,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Диалог настроек CodeLens.
    /// </summary>
    internal class CodeLensSettingsDialog
    {
        private readonly VisualStudioExtensibility extensibility;

        public CodeLensSettingsDialog(VisualStudioExtensibility extensibility)
        {
            this.extensibility = extensibility;
        }

        /// <summary>
        /// Показывает диалог настроек.
        /// </summary>
        public async Task ShowAsync(CancellationToken cancellationToken)
        {
            var settings = CodeLensSettings.Instance;

            var options = new[]
            {
                $"Показывать сложность методов: {(settings.ShowComplexity ? "Да" : "Нет")}",
                $"Показывать количество строк: {(settings.ShowLinesOfCode ? "Да" : "Нет")}",
                $"Показывать количество параметров: {(settings.ShowParameterCount ? "Да" : "Нет")}",
                $"Показывать информацию о классах: {(settings.ShowClassInfo ? "Да" : "Нет")}",
                $"Показывать количество ссылок: {(settings.ShowReferenceCount ? "Да" : "Нет")}",
                "Применить настройки"
            };            // Показываем настройки в виде сообщения
            var settingsMessage = string.Join("\n", options);
            
            await this.extensibility.Shell().ShowPromptAsync(
                $"Настройки CodeLens:\n\n{settingsMessage}",
                PromptOptions.OK,
                cancellationToken);

            // Пока упрощенная версия - показываем только текущие настройки
            // В будущем можно добавить интерактивное изменение настроек
        }

        /// <summary>
        /// Переключает настройку по индексу.
        /// </summary>
        private void ToggleSetting(int index)
        {
            var settings = CodeLensSettings.Instance;

            switch (index)
            {
                case 0:
                    settings.ShowComplexity = !settings.ShowComplexity;
                    break;
                case 1:
                    settings.ShowLinesOfCode = !settings.ShowLinesOfCode;
                    break;
                case 2:
                    settings.ShowParameterCount = !settings.ShowParameterCount;
                    break;
                case 3:
                    settings.ShowClassInfo = !settings.ShowClassInfo;
                    break;
                case 4:
                    settings.ShowReferenceCount = !settings.ShowReferenceCount;
                    break;
            }

            settings.Save();
        }
    }

    /// <summary>
    /// Настройки CodeLens.
    /// </summary>
    public class CodeLensSettings
    {
        private static CodeLensSettings? instance;
        private static readonly object lockObject = new object();

        /// <summary>
        /// Singleton instance настроек.
        /// </summary>
        public static CodeLensSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new CodeLensSettings();
                            instance.Load();
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// Показывать сложность методов.
        /// </summary>
        public bool ShowComplexity { get; set; } = true;

        /// <summary>
        /// Показывать количество строк кода.
        /// </summary>
        public bool ShowLinesOfCode { get; set; } = true;

        /// <summary>
        /// Показывать количество параметров.
        /// </summary>
        public bool ShowParameterCount { get; set; } = true;

        /// <summary>
        /// Показывать информацию о классах.
        /// </summary>
        public bool ShowClassInfo { get; set; } = true;

        /// <summary>
        /// Показывать количество ссылок.
        /// </summary>
        public bool ShowReferenceCount { get; set; } = false; // По умолчанию отключено для производительности

        /// <summary>
        /// Показывать AI предложения.
        /// </summary>
        public bool ShowAISuggestions { get; set; } = true;

        /// <summary>
        /// Минимальная сложность для отображения предупреждения.
        /// </summary>
        public int ComplexityWarningThreshold { get; set; } = 10;

        /// <summary>
        /// Максимальное количество строк метода без предупреждения.
        /// </summary>
        public int MaxMethodLines { get; set; } = 50;

        /// <summary>
        /// Загружает настройки (заглушка).
        /// </summary>
        public void Load()
        {
            // Здесь можно добавить загрузку из реестра или файла конфигурации
        }

        /// <summary>
        /// Сохраняет настройки (заглушка).
        /// </summary>
        public void Save()
        {
            // Здесь можно добавить сохранение в реестр или файл конфигурации
        }
    }
}
