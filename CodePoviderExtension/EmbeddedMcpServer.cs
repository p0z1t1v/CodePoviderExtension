using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodePoviderExtension.MCP;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Extensibility;

namespace CodeProviderExtension.MCP
{
    /// <summary>
    /// Встроенный MCP сервер для демонстрации и локального тестирования.
    /// </summary>
    public class EmbeddedMcpServer : IDisposable
    {
        private readonly ILogger<EmbeddedMcpServer> logger;
        private readonly ICodeAnalysisService codeAnalysisService;
        private readonly ICodeGenerationService codeGenerationService;
        private readonly VisualStudioExtensibility extensibility;

        private bool isRunning;

        public EmbeddedMcpServer(
            ILogger<EmbeddedMcpServer> logger,
            ICodeAnalysisService codeAnalysisService,
            ICodeGenerationService codeGenerationService,
            VisualStudioExtensibility extensibility)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
            this.codeGenerationService = codeGenerationService ?? throw new ArgumentNullException(nameof(codeGenerationService));
            this.extensibility = extensibility ?? throw new ArgumentNullException(nameof(extensibility));
        }

        public bool IsRunning => isRunning;

        public async Task StartAsync(CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Запуск встроенного MCP сервера...");
                
                // Здесь будет логика запуска HTTP сервера
                // Для демонстрации пока просто помечаем как запущенный
                isRunning = true;
                
                logger.LogInformation("Встроенный MCP сервер запущен на порту 3001");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка запуска встроенного MCP сервера");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            try
            {
                if (isRunning)
                {
                    logger.LogInformation("Остановка встроенного MCP сервера...");
                    isRunning = false;
                    logger.LogInformation("Встроенный MCP сервер остановлен");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка остановки встроенного MCP сервера");
            }
        }

        #region MCP Resources Implementation

        public async Task<IEnumerable<McpResource>> GetResourcesAsync(CancellationToken ct = default)
        {
            try
            {
                var resources = new List<McpResource>();

                // Добавляем ресурсы открытых файлов
                var openFiles = await GetOpenFilesAsync(ct);
                resources.AddRange(openFiles);

                // Добавляем контекстные ресурсы
                resources.Add(new McpResource
                {
                    Uri = "context://project-info",
                    Name = "Информация о проекте",
                    Description = "Общая информация о текущем проекте",
                    MimeType = "application/json"
                });

                resources.Add(new McpResource
                {
                    Uri = "context://workspace-settings",
                    Name = "Настройки рабочей области",
                    Description = "Конфигурация текущей рабочей области",
                    MimeType = "application/json"
                });

                return resources;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения ресурсов");
                return new List<McpResource>();
            }
        }

        public async Task<string> ReadResourceAsync(string uri, CancellationToken ct = default)
        {
            try
            {
                logger.LogDebug("Чтение ресурса: {Uri}", uri);

                return uri switch
                {
                    var u when u.StartsWith("file://") => await ReadFileResourceAsync(u, ct),
                    "context://project-info" => GetProjectInfo(ct),
                    "context://workspace-settings" => GetWorkspaceSettings(ct),
                    _ => throw new ArgumentException($"Неподдерживаемый URI ресурса: {uri}")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка чтения ресурса {Uri}", uri);
                return string.Empty;
            }
        }

        #endregion

        #region MCP Tools Implementation

        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken ct = default)
        {
            try
            {
                logger.LogDebug("Выполнение инструмента: {ToolName}", toolName);

                return toolName switch
                {
                    "analyze-code" => await AnalyzeCodeToolAsync(arguments, ct),
                    "generate-code" => await GenerateCodeToolAsync(arguments, ct),
                    "refactor-code" => await RefactorCodeToolAsync(arguments, ct),
                    "find-patterns" => await FindPatternsToolAsync(arguments, ct),
                    "get-context" => await GetContextToolAsync(arguments, ct),
                    _ => throw new ArgumentException($"Неизвестный инструмент: {toolName}")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка выполнения инструмента {ToolName}", toolName);
                return new { error = ex.Message };
            }
        }

        #endregion

        #region Helper Methods

        private Task<IEnumerable<McpResource>> GetOpenFilesAsync(CancellationToken ct)
        {
            try
            {
                // Здесь будет логика получения открытых файлов из VS
                // Пока возвращаем заглушку
                return Task.FromResult<IEnumerable<McpResource>>(new List<McpResource>
                {
                    new McpResource
                    {
                        Uri = "file://current-document",
                        Name = "Текущий документ",
                        Description = "Активный документ в редакторе",
                        MimeType = "text/plain"
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка получения открытых файлов");
                return Task.FromResult<IEnumerable<McpResource>>(new List<McpResource>());
            }
        }

        private Task<string> ReadFileResourceAsync(string uri, CancellationToken ct)
        {
            // Упрощенная реализация чтения файлов
            if (uri == "file://current-document")
            {
                return Task.FromResult("// Содержимое текущего документа\n// Здесь будет реальный код из активного редактора");
            }

            return Task.FromResult(string.Empty);
        }

        private string GetProjectInfo(CancellationToken ct)
        {
            var projectInfo = new
            {
                name = "CodeProviderExtension",
                type = "Visual Studio Extension",
                framework = ".NET 9.0",
                language = "C#",
                features = new[] { "Code Analysis", "Code Generation", "CodeLens", "MCP Integration" }
            };

            return System.Text.Json.JsonSerializer.Serialize(projectInfo, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string GetWorkspaceSettings(CancellationToken ct)
        {
            var settings = new
            {
                mcpEnabled = true,
                codeLensEnabled = true,
                autoAnalysis = true,
                aiIntegration = "enabled"
            };

            return System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private async Task<object> AnalyzeCodeToolAsync(Dictionary<string, object> arguments, CancellationToken ct)
        {
            var code = arguments.GetValueOrDefault("code")?.ToString() ?? "";
            var language = arguments.GetValueOrDefault("language")?.ToString() ?? "csharp";

            var result = await codeAnalysisService.AnalyzeCodeAsync(code, language, ct);
            
            return new
            {
                language = result.Language,
                complexity = result.ComplexityScore,
                lines = result.LineCount,
                classes = result.Classes.Count(),
                methods = result.Methods.Count(),
                issues = result.Issues.Count()
            };
        }

        private async Task<object> GenerateCodeToolAsync(Dictionary<string, object> arguments, CancellationToken ct)
        {
            var description = arguments.GetValueOrDefault("description")?.ToString() ?? "";
            var language = arguments.GetValueOrDefault("language")?.ToString() ?? "csharp";

            var generatedCode = await codeGenerationService.GenerateCodeAsync(description, language, ct);
            
            return new
            {
                code = generatedCode,
                language = language,
                description = description
            };
        }

        private async Task<object> RefactorCodeToolAsync(Dictionary<string, object> arguments, CancellationToken ct)
        {
            var code = arguments.GetValueOrDefault("code")?.ToString() ?? "";
            var instructions = arguments.GetValueOrDefault("instructions")?.ToString() ?? "";

            var refactoredCode = await codeGenerationService.RefactorCodeAsync(code, instructions, ct);
            
            return new
            {
                originalCode = code,
                refactoredCode = refactoredCode,
                instructions = instructions
            };
        }

        private async Task<object> FindPatternsToolAsync(Dictionary<string, object> arguments, CancellationToken ct)
        {
            // Заглушка для поиска паттернов в проекте
            return new
            {
                patterns = new[]
                {
                    "Command Pattern (Visual Studio Commands)",
                    "Dependency Injection (Services)",
                    "Observer Pattern (Events)",
                    "Strategy Pattern (Code Analysis)"
                }
            };
        }

        private async Task<object> GetContextToolAsync(Dictionary<string, object> arguments, CancellationToken ct)
        {
            return new
            {
                projectType = "Visual Studio Extension",
                technologies = new[] { "C#", "VisualStudio.Extensibility", "MCP" },
                currentFile = "Unknown",
                openFiles = 1
            };
        }

        #endregion

        public void Dispose()
        {
            if (isRunning)
            {
                _ = StopAsync(CancellationToken.None);
            }
            logger.LogDebug("EmbeddedMcpServer освобожден");
        }
    }
}