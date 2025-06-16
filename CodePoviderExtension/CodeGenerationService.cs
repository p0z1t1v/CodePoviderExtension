using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace CodeProviderExtension
{
    /// <summary>
    /// Сервис для генерации кода с использованием искусственного интеллекта.
    /// </summary>
    internal class CodeGenerationService : ICodeGenerationService
    {
        private readonly HttpClient httpClient;
        private readonly ICodeAnalysisService codeAnalysisService;

        public CodeGenerationService(HttpClient httpClient, ICodeAnalysisService codeAnalysisService)
        {
            this.httpClient = httpClient;
            this.codeAnalysisService = codeAnalysisService;
        }

        public Task<string> GenerateCodeAsync(string prompt, string language, CancellationToken cancellationToken = default)
        {
            try
            {
                // Для демонстрации генерируем простой шаблон кода
                var result = language.ToLowerInvariant() switch
                {
                    "csharp" or "cs" => GenerateCSharpTemplate(prompt),
                    "javascript" or "js" => GenerateJavaScriptTemplate(prompt),
                    //"python" or "py" => GeneratePythonTemplate(prompt),
                    _ => GenerateGenericTemplate(prompt, language)
                };
                
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult($"// Ошибка генерации кода: {ex.Message}");
            }
        }

        public async Task<string> RefactorCodeAsync(string code, string instructions, CancellationToken cancellationToken = default)
        {
            try
            {
                // Анализируем существующий код
                var analysisResult = await codeAnalysisService.AnalyzeCodeAsync(code, "csharp", cancellationToken);
                
                // Применяем базовые рефакторинги
                var refactoredCode = ApplyBasicRefactoring(code, instructions, analysisResult);
                
                return refactoredCode;
            }
            catch (Exception ex)
            {
                return $"// Ошибка рефакторинга: {ex.Message}\n\n{code}";
            }
        }

        private string GenerateCSharpTemplate(string prompt)
        {
            if (prompt.Contains("класс", StringComparison.OrdinalIgnoreCase) || 
                prompt.Contains("class", StringComparison.OrdinalIgnoreCase))
            {
                return CodeTemplates.GenerateClassTemplate(prompt);
            }
            
            if (prompt.Contains("метод", StringComparison.OrdinalIgnoreCase) || 
                prompt.Contains("method", StringComparison.OrdinalIgnoreCase))
            {
                return CodeTemplates.GenerateMethodTemplate(prompt);
            }
            
            return CodeTemplates.GenerateGenericCSharpTemplate(prompt);
        }

        private string GenerateJavaScriptTemplate(string prompt)
        {
            return $@"// Сгенерированный JavaScript код на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

function generatedFunction() {{
    // TODO: Добавьте код здесь
    console.log('JavaScript код сгенерирован успешно!');
}}

// Экспорт функции (если используется модульная система)
// module.exports = generatedFunction;";
        }

//        private string GeneratePythonTemplate(string prompt)
//        {
//            return $@"# Сгенерированный Python код на основе запроса: {prompt}
//# TODO: Реализуйте требуемую функциональность

//def generated_function():
//    \"\"\"
//    TODO: Добавьте описание функции
//    \"\"\"
//    # TODO: Добавьте код здесь
//    print('Python код сгенерирован успешно!')

//if __name__ == '__main__':
//    generated_function()";
//        }

        private string GenerateGenericTemplate(string prompt, string language)
        {
            return $@"// Сгенерированный код ({language}) на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

// Добавьте ваш код здесь
// Код успешно сгенерирован для языка: {language}";
        }

        private string ApplyBasicRefactoring(string code, string instructions, CodeAnalysisResult analysisResult)
        {
            var refactoredCode = code;
            var instructionsLower = instructions.ToLowerInvariant();

            // Применяем различные типы рефакторинга на основе инструкций
            if (instructionsLower.Contains("производительность") || instructionsLower.Contains("performance"))
            {
                refactoredCode = ApplyPerformanceRefactoring(refactoredCode);
            }

            if (instructionsLower.Contains("читаемость") || instructionsLower.Contains("readability"))
            {
                refactoredCode = ApplyReadabilityRefactoring(refactoredCode);
            }

            if (instructionsLower.Contains("обработка ошибок") || instructionsLower.Contains("error handling"))
            {
                refactoredCode = AddErrorHandling(refactoredCode);
            }

            if (instructionsLower.Contains("упростить") || instructionsLower.Contains("simplify"))
            {
                refactoredCode = SimplifyCode(refactoredCode);
            }

            // Добавляем комментарий о примененных изменениях
            var header = $"// Рефакторинг применен: {instructions}\n// Обнаружено проблем: {analysisResult.Issues.Count()}\n\n";
            
            return header + refactoredCode;
        }

        private string ApplyPerformanceRefactoring(string code)
        {
            // Простые оптимизации производительности
            code = code.Replace("string += ", "stringBuilder.Append(");
            code = code.Replace("new List<>()", "new List<>() // TODO: Укажите начальную емкость если известна");
            
            return code;
        }

        private string ApplyReadabilityRefactoring(string code)
        {
            // Улучшения читаемости
            var lines = code.Split('\n');
            var result = new StringBuilder();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Добавляем комментарии к сложным строкам
                if (trimmedLine.Contains("&&") || trimmedLine.Contains("||"))
                {
                    result.AppendLine("        // TODO: Рассмотрите вынесение условия в отдельную переменную");
                }
                
                result.AppendLine(line);
            }
            
            return result.ToString();
        }

        private string AddErrorHandling(string code)
        {
            // Базовое добавление обработки ошибок
            if (!code.Contains("try") && !code.Contains("catch"))
            {
                return $@"try
{{
{code}
}}
catch (Exception ex)
{{
    // TODO: Добавьте соответствующую обработку ошибок
    throw new InvalidOperationException(""Операция не может быть выполнена"", ex);
}}";
            }
            
            return code;
        }

        private string SimplifyCode(string code)
        {
            // Простые упрощения кода
            code = code.Replace("if (condition == true)", "if (condition)");
            code = code.Replace("if (condition == false)", "if (!condition)");
            code = code.Replace("return true;\n    }\n    else\n    {\n        return false;", "return condition;");
            
            return code;
        }
    }
}
