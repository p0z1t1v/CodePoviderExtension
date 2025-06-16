namespace CodeProviderExtension
{
    /// <summary>
    /// Вспомогательные методы для генерации шаблонов кода.
    /// </summary>
    internal static class CodeTemplates
    {
        public static string GenerateClassTemplate(string prompt)
        {
            var className = ExtractClassName(prompt);
            
            return $@"using System;

namespace YourNamespace
{{
    /// <summary>
    /// {className} - автоматически сгенерированный класс.
    /// TODO: Добавьте описание класса
    /// </summary>
    public class {className}
    {{
        // TODO: Добавьте поля класса

        /// <summary>
        /// Конструктор класса {className}.
        /// </summary>
        public {className}()
        {{
            // TODO: Инициализация
        }}

        // TODO: Добавьте методы класса
    }}
}}";
        }

        public static string GenerateMethodTemplate(string prompt)
        {
            var methodName = ExtractMethodName(prompt);
            
            return $@"/// <summary>
/// {methodName} - автоматически сгенерированный метод.
/// TODO: Добавьте описание метода
/// </summary>
/// <returns>TODO: Описание возвращаемого значения</returns>
public void {methodName}()
{{
    // TODO: Реализация метода
    throw new NotImplementedException();
}}";
        }

        public static string GenerateGenericCSharpTemplate(string prompt)
        {
            return $@"// Сгенерированный код на основе запроса: {prompt}
// TODO: Реализуйте требуемую функциональность

using System;

public class GeneratedCode
{{
    public void Execute()
    {{
        // TODO: Добавьте код здесь
        Console.WriteLine(""Код сгенерирован успешно!"");
    }}
}}";
        }

        private static string ExtractClassName(string prompt)
        {
            // Простое извлечение имени класса из подсказки
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (char.IsUpper(word[0]) && word.Length > 2)
                    return word;
            }
            return "GeneratedClass";
        }

        private static string ExtractMethodName(string prompt)
        {
            // Простое извлечение имени метода из подсказки
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.Length > 3 && !word.Contains("метод") && !word.Contains("method"))
                    return char.ToUpper(word[0]) + word.Substring(1);
            }
            return "GeneratedMethod";
        }
    }
}
