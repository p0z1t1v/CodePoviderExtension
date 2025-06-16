using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using CodePoviderExtension.MCP;

namespace CodeProviderExtension
{
    /// <summary>
    /// Простая команда для тестирования MCP подключения
    /// </summary>
    [VisualStudioContribution]
    internal class McpTestCommand : Command
    {
        private readonly TraceSource logger;
        private readonly IMcpClient mcpClient;

        public McpTestCommand(
            VisualStudioExtensibility extensibility,
            TraceSource traceSource,
            IMcpClient mcpClient) : base(extensibility)
        {
            this.logger = traceSource ?? throw new ArgumentNullException(nameof(traceSource));
            this.mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        }

        public override CommandConfiguration CommandConfiguration => new("🧠 Тест MCP")
        {
            TooltipText = "Тестирование подключения к MCP серверу",
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu]
        };

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
        {
            try
            {
                logger.TraceInformation("Тестирование MCP соединения");

                // Простой тест подключения
                if (!mcpClient.IsConnected)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "Инициализация MCP...", 
                        PromptOptions.OK, ct);
                    
                    try
                    {
                        await mcpClient.InitializeAsync(ct);
                    }
                    catch (Exception initEx)
                    {
                        await this.Extensibility.Shell().ShowPromptAsync(
                            $"❌ Ошибка инициализации MCP: {initEx.Message}\n\nЗапустите my-memory сервер:\nnpx @modelcontextprotocol/server-memory", 
                            PromptOptions.OK, ct);
                        return;
                    }
                }

                if (mcpClient.IsConnected)
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "✅ MCP подключение успешно!\n\nmy-memory сервер доступен.", 
                        PromptOptions.OK, ct);
                }
                else
                {
                    await this.Extensibility.Shell().ShowPromptAsync(
                        "❌ Ошибка подключения к MCP\n\nЗапустите my-memory сервер:\nnpx @modelcontextprotocol/server-memory", 
                        PromptOptions.OK, ct);
                }
            }
            catch (Exception ex)
            {
                logger.TraceInformation($"Ошибка при тестировании MCP: {ex.Message}");
                await this.Extensibility.Shell().ShowPromptAsync(
                    $"Ошибка MCP: {ex.Message}", 
                    PromptOptions.OK, ct);
            }
        }
    }
}
