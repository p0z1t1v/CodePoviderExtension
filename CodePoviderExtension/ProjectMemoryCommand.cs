using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.Editor;
using CodePoviderExtension.MCP;

namespace CodeProviderExtension
{
    /// <summary>
    /// Команда для управления памятью проекта через MCP my-memory сервер
    /// </summary>
    [VisualStudioContribution]
    internal class ProjectMemoryCommand : Command
    {
        private readonly TraceSource logger;
        private readonly IMcpClient mcpClient;

        public ProjectMemoryCommand(
            VisualStudioExtensibility extensibility,
            TraceSource traceSource,
            IMcpClient mcpClient) : base(extensibility)
        {
            this.logger = traceSource ?? throw new ArgumentNullException(nameof(traceSource));
            this.mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        }

        public override CommandConfiguration CommandConfiguration => new("🧠 Память проекта")
        {
            TooltipText = "Управление памятью и контекстом проекта через MCP",
            Placements = new[] { CommandPlacement.KnownPlacements.ExtensionsMenu }
        };

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
        {
            try
            {
                logger.TraceEvent(TraceEventType.Information, 0, "Начало работы с памятью проекта");

                // Инициализируем MCP клиент если еще не инициализирован
                if (!mcpClient.IsConnected)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Инициализация MCP...", 
                        PromptOptions.OK, ct);
                    
                    await mcpClient.InitializeAsync(ct);
                }

                // Проверяем подключение к my-memory серверу
                if (mcpClient.IsConnected)
                {
                    await ShowProjectMemoryDialog(context, ct);
                }
                else
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Ошибка: Не удается подключиться к MCP серверу my-memory. Убедитесь, что сервер запущен на http://localhost:3000", 
                        PromptOptions.OK, ct);
                }
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при работе с памятью проекта: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка: {ex.Message}", 
                    PromptOptions.OK, ct);
            }
        }

        private async Task ShowProjectMemoryDialog(IClientContext context, CancellationToken ct)
        {
            try
            {
                // Получаем статистику памяти
                var toolRequest = new Dictionary<string, object>
                {
                    { "name", "GetMemoryStatistics" },
                    { "arguments", new Dictionary<string, object>() }
                };

                var result = await mcpClient.Tools.CallToolAsync("GetMemoryStatistics", toolRequest, ct);
                
                if (result != null)
                {
                    var message = $"📊 Статистика памяти проекта:\n\n{result}";
                    
                    var choice = await this.Extensibility.Shell().ShowPromptAsync(
                        message, 
                        PromptOptions.OKCancel, 
                        ct);

                    if (choice)
                    {
                        await ShowMemoryManagement(context, ct);
                    }
                }
                else
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Не удалось получить статистику памяти. Возможно, my-memory сервер недоступен.", 
                        PromptOptions.OK, ct);
                }
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при получении статистики памяти: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    "Не удалось подключиться к серверу памяти. Запустите my-memory сервер командой:\nnpx @modelcontextprotocol/server-memory", 
                    PromptOptions.OK, ct);
            }
        }

        private async Task ShowMemoryManagement(IClientContext context, CancellationToken ct)
        {
            var choice = await this.Extensibility.Shell().ShowPromptAsync(
                "Выберите действие:\n\n1. Сохранить текущий код в память\n2. Найти похожий код\n3. Очистить память проекта\n4. Экспорт памяти", 
                PromptOptions.OKCancel, 
                ct);

            if (choice)
            {
                await SaveCurrentCodeToMemory(context, ct);
            }
        }

        private async Task SaveCurrentCodeToMemory(IClientContext context, CancellationToken ct)
        {
            try
            {
                // Получаем активное представление
                var activeTextView = await this.Extensibility.Editor().GetActiveTextViewAsync(context, ct);
                if (activeTextView == null)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Нет активного файла для сохранения",
                        PromptOptions.OK, ct);
                    return;
                }

                var documentSnapshot = activeTextView.Document;
                var contentRange = documentSnapshot.Text; // Получаем полный диапазон текста документа
                var content = contentRange.CopyToString(); // Используем метод расширения CopyToString для TextRange
                var fileName = documentSnapshot.Uri.LocalPath;

                // Сохраняем в my-memory
                var saveRequest = new Dictionary<string, object>
                {
                    { "title", $"Файл: {System.IO.Path.GetFileName(fileName)}" },
                    { "content", content },
                    { "projectId", "CodeProviderExtension" },
                    { "type", "CodeSnippet" },
                    { "metadata", System.Text.Json.JsonSerializer.Serialize(new {
                        filePath = fileName,
                        language = GetLanguageFromFileName(fileName),
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }) }
                };

                var result = await mcpClient.Tools.CallToolAsync("SaveProjectArtifact", saveRequest, ct);

                await this.Extensibility.Shell().ShowPromptAsync(
                    $"✅ Код успешно сохранен в память проекта!\n\nФайл: {System.IO.Path.GetFileName(fileName)}\nРазмер: {content.Length} символов",
                    PromptOptions.OK, ct);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка при сохранении кода: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка при сохранении: {ex.Message}",
                    PromptOptions.OK, ct);
            }
        }

        private async Task SearchInMemory(IClientContext context, CancellationToken ct)
        {
            try
            {
                var searchRequest = new Dictionary<string, object>
                {
                    { "query", "код" },
                    { "projectId", "CodeProviderExtension" },
                    { "maxResults", 5 }
                };

                var result = await mcpClient.Tools.CallToolAsync("SearchProjectArtifacts", searchRequest, ct);
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"🔍 Результаты поиска:\n\n{result}", 
                    PromptOptions.OK, ct);
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, $"Ошибка поиска: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка поиска: {ex.Message}", 
                    PromptOptions.OK, ct);
            }
        }

        private async Task ClearProjectMemory(IClientContext context, CancellationToken ct)
        {
            var confirm = await this.Extensibility.Shell().ShowPromptAsync(
                "⚠️ Вы уверены, что хотите очистить всю память проекта?", 
                PromptOptions.OKCancel, ct);

            if (confirm)
            {
                try
                {
                    var clearRequest = new Dictionary<string, object> { { "sessionId", "CodeProviderExtension" } };
                    await mcpClient.Tools.CallToolAsync("ClearChatSession", clearRequest, ct);
                    
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "✅ Память проекта очищена", 
                        PromptOptions.OK, ct);
                }
                catch (Exception ex)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        $"Ошибка очистки: {ex.Message}", 
                        PromptOptions.OK, ct);
                }
            }
        }

        private async Task ExportProjectMemory(IClientContext context, CancellationToken ct)
        {
            try
            {
                var exportRequest = new Dictionary<string, object>
                {
                    { "projectId", "CodeProviderExtension" },
                    { "format", "markdown" },
                    { "includeMetadata", true }
                };

                var result = await mcpClient.Tools.CallToolAsync("ExportProject", exportRequest, ct);
                
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"📄 Экспорт памяти проекта:\n\n{result}", 
                    PromptOptions.OK, ct);
            }
            catch (Exception ex)
            {
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка экспорта: {ex.Message}", 
                    PromptOptions.OK, ct);
            }
        }

        private static string GetLanguageFromFileName(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".cpp" or ".cc" or ".cxx" => "cpp",
                ".c" => "c",
                ".html" => "html",
                ".css" => "css",
                ".json" => "json",
                ".xml" => "xml",
                _ => "text"
            };
        }
    }
}
