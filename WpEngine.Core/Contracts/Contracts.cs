using WpEngine.Core.AST;

namespace WpEngine.Core.Contracts;

/// <summary>
/// Элемент пошагового протокола вычисления слабейшего предусловия.
/// Фиксирует состояние формулы после прохождения через отдельный оператор.
/// </summary>
/// <param name="StatementCode">Код текущего обрабатываемого оператора программы.</param>
/// <param name="IntermediateCondition">Промежуточное условие после шага подстановки/ветвления.</param>
/// <param name="Description">Академическое пояснение выполненного шага для отчета.</param>
public record WpStep(
    string StatementCode,
    string IntermediateCondition,
    string Description
);

/// <summary>
/// Полный агрегированный результат работы калькулятора wp.
/// Содержит финальное дерево, строковые представления для GUI и отчетный трейс.
/// </summary>
/// <param name="FinalCondition">Синтаксическое дерево (AST) полученного предусловия.</param>
/// <param name="FinalConditionShort">Краткая математическая нотация формулы.</param>
/// <param name="FinalConditionHuman">Интерпретация условия на естественном русском языке.</param>
/// <param name="HoareTriadText">Текстовое представление финальной триады Хоара: {Pre} P {Post}.</param>
/// <param name="Trace">Список промежуточных шагов вычисления (от конца к началу).</param>
public record WpResult(
    Condition FinalCondition,
    string FinalConditionShort,
    string FinalConditionHuman,
    string HoareTriadText,
    List<WpStep> Trace
);

/// <summary>
/// Интерфейс вычислительного ядра wp.
/// Контракт для реализации Человеком 2 и использования в интерфейсе Человеком 4.
/// </summary>
public interface IWpEngine
{
    /// <summary>
    /// Вычисляет слабейшее предусловие для переданного фрагмента программы и постусловия.
    /// </summary>
    WpResult CalculateWp(Statement program, Condition postCondition);
}

/// <summary>
/// Интерфейс транслятора (парсера) пользовательского ввода.
/// Контракт для реализации Человеком 3.
/// </summary>
public interface ICodeParser
{
    /// <summary>
    /// Преобразует исходный текст программы в синтаксическое дерево Statement.
    /// </summary>
    Statement ParseProgram(string code);

    /// <summary>
    /// Преобразует текстовую запись постусловия в предикат Condition.
    /// </summary>
    Condition ParseCondition(string conditionText);
}