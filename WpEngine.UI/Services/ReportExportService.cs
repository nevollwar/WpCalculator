using System.IO;
using System.Text;
using WpEngine.UI.ViewModels;

namespace WpEngine.UI.Services;

/// <summary>
/// Сервис экспорта результатов вычисления wp в файл Markdown (.md).
/// Формат совместим с GitHub, Obsidian, Typora, а через Pandoc — с Word.
/// </summary>
public static class ReportExportService
{
    public static string BuildMarkdown(MainViewModel vm)
    {
        var sb = new StringBuilder();
        string date = DateTime.Now.ToString("dd.MM.yyyy");
        string time = DateTime.Now.ToString("HH:mm");

        // ОБЛОЖКА

        sb.AppendLine("# Отчёт по лабораторной работе № 2");
        sb.AppendLine();
        sb.AppendLine("## Вычисление слабейшего предусловия `wp(P, R)`");
        sb.AppendLine();
        sb.AppendLine("| | |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| **Дисциплина** | Теоретичсекая информатика |");
        sb.AppendLine("| **Университет** | ТвГТУ |");
        sb.AppendLine("| **Группа** | Б.ПИН.ИИ.25.16 |");
        sb.AppendLine("| **Дата** | " + date + " |");
        sb.AppendLine("| **Время** | " + time + " |");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(vm.ElapsedTimeText))
            sb.AppendLine($">  Время вычисления: **{vm.ElapsedTimeText}**");
        else
            sb.AppendLine($">  Сформировано автоматически калькулятором wp");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();


        // 1. ВХОДНЫЕ ДАННЫЕ
  
        sb.AppendLine("## 1. Входные данные");
        sb.AppendLine();

        sb.AppendLine("### 1.1 Постусловие `R`");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(vm.PostConditionText);
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(vm.PostConditionHumanText))
        {
            sb.AppendLine($">  **Своими словами:** {vm.PostConditionHumanText}");
            sb.AppendLine();
        }

        sb.AppendLine("### 1.2 Программа `P`");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(vm.ProgramCodeText);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();


        // 2. РЕЗУЛЬТАТ: wp(P, R)

        sb.AppendLine("## 2. Слабейшее предусловие `wp(P, R)`");
        sb.AppendLine();

        sb.AppendLine("### 2.1 Математическая запись");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(vm.FinalConditionShort);
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(vm.FinalConditionHuman))
        {
            sb.AppendLine("### 2.2 Пояснение");
            sb.AppendLine();
            sb.AppendLine(vm.FinalConditionHuman);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(vm.FinalConditionLatex))
        {
            sb.AppendLine("### 2.3 LaTeX (для вставки в отчёт / Overleaf)");
            sb.AppendLine();
            sb.AppendLine("```latex");
            sb.AppendLine($"$$ {vm.FinalConditionLatex} $$");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // 3. ТРИАДА ХОАРА

        if (!string.IsNullOrWhiteSpace(vm.HoareTriadText))
        {
            sb.AppendLine("## 3. Триада Хоара `{ Pre } P { Post }`");
            sb.AppendLine();
            sb.AppendLine("> Контракт корректности: если до старта выполнено `{Pre}`,");
            sb.AppendLine("> то после выполнения `P` гарантированно выполняется `{Post}`.");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(vm.HoareTriadText);
            sb.AppendLine("```");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(vm.HoareTriadLatex))
            {
                sb.AppendLine("```latex");
                sb.AppendLine($"$$ {vm.HoareTriadLatex} $$");
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

  
        // 4. УСЛОВИЕ ОПРЕДЕЛЁННОСТИ

        if (vm.HasDivisionConstraint && !string.IsNullOrWhiteSpace(vm.DivisionConstraintMessage))
        {
            sb.AppendLine("## 4. Условие определённости");
            sb.AppendLine();
            sb.AppendLine(">  **В программе обнаружено деление.**");
            sb.AppendLine("> Движок автоматически добавил защиту от деления на ноль.");
            sb.AppendLine();
            sb.AppendLine($"**{vm.DivisionConstraintMessage}**");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // 5. ПОШАГОВЫЙ ТРЕЙС

        if (vm.TraceSteps.Count > 0)
        {
            int sectionNum = vm.HasDivisionConstraint ? 5 : 4;
            sb.AppendLine($"## {sectionNum}. Пошаговое вычисление (обратный ход)");
            sb.AppendLine();
            sb.AppendLine("Правило композиции Дейкстры:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("wp(S1; S2, R) = wp(S1, wp(S2, R))");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Вычисление идёт **снизу вверх** — от последнего оператора к первому.");
            sb.AppendLine();

            // Сводная таблица шагов
            sb.AppendLine("### Сводная таблица");
            sb.AppendLine();
            sb.AppendLine("| № | Оператор | Промежуточное условие |");
            sb.AppendLine("|---|---|---|");
            foreach (var step in vm.TraceSteps)
            {
                string op   = EscapeMd(step.StatementCode);
                string cond = EscapeMd(step.IntermediateCondition);
                sb.AppendLine($"| {step.StepIndex} | `{op}` | `{cond}` |");
            }
            sb.AppendLine();

            // Детальные шаги
            sb.AppendLine("### Детальный разбор");
            sb.AppendLine();
            foreach (var step in vm.TraceSteps)
            {
                sb.AppendLine($"#### Шаг {step.StepIndex} — `{EscapeMd(step.StatementCode)}`");
                sb.AppendLine();
                sb.AppendLine("**Промежуточное условие после подстановки:**");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(step.IntermediateCondition);
                sb.AppendLine("```");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(step.Description))
                {
                    sb.AppendLine($"**Пояснение:** {step.Description}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(step.RealLifeHint))
                {
                    sb.AppendLine($">  {step.RealLifeHint}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }


        // ИТОГОВАЯ СВОДКА

        sb.AppendLine("## Итог");
        sb.AppendLine();
        sb.AppendLine("| Параметр | Значение |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Постусловие `R` | `{EscapeMd(vm.PostConditionText)}` |");
        sb.AppendLine($"| Слабейшее предусловие `wp` | `{EscapeMd(vm.FinalConditionShort)}` |");
        sb.AppendLine($"| Шагов трейса | {vm.TraceSteps.Count} |");
        sb.AppendLine($"| Деление на ноль | {(vm.HasDivisionConstraint ? "  есть, добавлена защита" : "  нет")} |");
        if (!string.IsNullOrWhiteSpace(vm.ElapsedTimeText))
            sb.AppendLine($"| Время расчёта | {vm.ElapsedTimeText} |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Отчёт сформирован автоматически — WpCalculator, ТвГТУ, группа Б.ПИН.ИИ.25.16*");

        return sb.ToString();
    }

    public static void ExportToFile(MainViewModel vm, string filePath)
    {
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string content = BuildMarkdown(vm);
        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    // Экранирует | и ` для использования внутри Markdown-таблиц
    private static string EscapeMd(string text) =>
        text.Replace("|", "\\|").Replace("`", "'");
}
