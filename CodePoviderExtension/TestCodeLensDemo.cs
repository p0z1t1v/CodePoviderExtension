using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeProviderExtension.Test
{
    /// <summary>
    /// Тестовый класс для демонстрации CodeLens функциональности.
    /// </summary>
    public class TestCodeLensClass
    {
        private readonly List<string> data;
        private int counter;

        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        public TestCodeLensClass()
        {
            data = new List<string>();
            counter = 0;
            Name = "Default";
            CreatedDate = DateTime.Now;
            IsActive = true;
        }

        /// <summary>
        /// Простой метод с низкой сложностью.
        /// </summary>
        public void SimpleMethod()
        {
            counter++;
            Name = $"Updated {counter}";
        }

        /// <summary>
        /// Метод со средней сложностью (несколько условий).
        /// </summary>
        public string ProcessData(string input, bool validate, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return "Empty input";

            if (validate)
            {
                if (input.Length > maxLength)
                {
                    return input.Substring(0, maxLength);
                }
                
                if (input.Contains("bad"))
                {
                    return "Invalid content";
                }
            }

            data.Add(input);
            return input.ToUpper();
        }

        /// <summary>
        /// Сложный метод с высокой цикломатической сложностью (ДЕМО).
        /// Этот метод специально сделан сложным для демонстрации CodeLens предупреждений.
        /// </summary>
        public string ComplexMethod(string input, int type, bool flag1, bool flag2, 
            int threshold, string category, DateTime date, List<string> items)
        {
            string result = string.Empty;

            // Много вложенных условий для увеличения сложности
            if (!string.IsNullOrEmpty(input))
            {
                if (type == 1)
                {
                    if (flag1)
                    {
                        if (input.Length > threshold)
                        {
                            if (category == "special")
                            {
                                result = input.ToUpper();
                            }
                            else if (category == "normal")
                            {
                                result = input.ToLower();
                            }
                            else
                            {
                                result = input;
                            }
                        }
                        else
                        {
                            result = "Short input";
                        }
                    }
                    else if (flag2)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i] == input)
                            {
                                result = $"Found at {i}";
                                break;
                            }
                        }
                    }
                    else
                    {
                        result = "No flags";
                    }
                }
                else if (type == 2)
                {
                    switch (category)
                    {
                        case "A":
                            result = ProcessTypeA(input, date);
                            break;
                        case "B":
                            result = ProcessTypeB(input, threshold);
                            break;
                        case "C":
                            result = ProcessTypeC(input, flag1, flag2);
                            break;
                        default:
                            result = "Unknown category";
                            break;
                    }
                }
                else if (type == 3)
                {
                    try
                    {
                        var processed = input.Trim();
                        if (processed.StartsWith("prefix_"))
                        {
                            processed = processed.Substring(7);
                        }
                        
                        if (date > DateTime.Now.AddDays(-30))
                        {
                            result = $"Recent: {processed}";
                        }
                        else
                        {
                            result = $"Old: {processed}";
                        }
                    }
                    catch (Exception ex)
                    {
                        result = $"Error: {ex.Message}";
                    }
                }
                else
                {
                    result = "Invalid type";
                }
            }
            else
            {
                result = "Empty input";
            }

            // Дополнительная логика для еще большей сложности
            if (result.Length > 50)
            {
                result = result.Substring(0, 47) + "...";
            }

            return result;
        }

        private string ProcessTypeA(string input, DateTime date)
        {
            return $"TypeA: {input} ({date:yyyy-MM-dd})";
        }

        private string ProcessTypeB(string input, int threshold)
        {
            return input.Length > threshold ? input.ToUpper() : input.ToLower();
        }

        private string ProcessTypeC(string input, bool flag1, bool flag2)
        {
            if (flag1 && flag2)
                return input.Reverse().ToString();
            else if (flag1)
                return input.ToUpper();
            else if (flag2)
                return input.ToLower();
            else
                return input;
        }

        /// <summary>
        /// Длинный метод для демонстрации предупреждения о количестве строк.
        /// </summary>
        public void VeryLongMethod()
        {
            // Много строк кода для демонстрации
            var line1 = "This is line 1";
            var line2 = "This is line 2";
            var line3 = "This is line 3";
            var line4 = "This is line 4";
            var line5 = "This is line 5";
            var line6 = "This is line 6";
            var line7 = "This is line 7";
            var line8 = "This is line 8";
            var line9 = "This is line 9";
            var line10 = "This is line 10";
            
            Console.WriteLine(line1);
            Console.WriteLine(line2);
            Console.WriteLine(line3);
            Console.WriteLine(line4);
            Console.WriteLine(line5);
            Console.WriteLine(line6);
            Console.WriteLine(line7);
            Console.WriteLine(line8);
            Console.WriteLine(line9);
            Console.WriteLine(line10);
            
            // Еще больше строк
            for (int i = 11; i <= 20; i++)
            {
                Console.WriteLine($"This is line {i}");
            }
            
            // И еще больше
            for (int i = 21; i <= 30; i++)
            {
                Console.WriteLine($"This is line {i}");
            }
            
            // И еще...
            for (int i = 31; i <= 40; i++)
            {
                Console.WriteLine($"This is line {i}");
            }
            
            // Много параметров в вызове
            ComplexMethod("test", 1, true, false, 10, "special", DateTime.Now, new List<string>());
            
            // Финальные строки
            Console.WriteLine("Method completed");
            counter += 100;
        }

        /// <summary>
        /// Метод с большим количеством параметров для демонстрации предупреждения.
        /// </summary>
        public void MethodWithManyParameters(string param1, int param2, bool param3, 
            DateTime param4, List<string> param5, Dictionary<string, object> param6,
            Func<string, bool> param7, Action<int> param8)
        {
            // Простая логика
            if (param3)
            {
                param8(param2);
            }
        }
    }

    /// <summary>
    /// Еще один класс для демонстрации статистики.
    /// </summary>
    public class AnotherTestClass
    {
        public string Property1 { get; set; }
        public int Property2 { get; set; }

        public void Method1() { }
        public void Method2() { }
    }
}
