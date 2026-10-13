using WpEngine.Core.AST;
using WpEngine.Core.Contracts;

namespace WpEngine.Core.Parsing;

/// <summary>
/// Реализация ICodeParser: превращает введённый пользователем текст в AST.
/// </summary>
public class CodeParser : ICodeParser
{
    /// <summary>
    /// Разбирает текст программы (присваивания, if-else, ";").
    /// </summary>
    /// <param name="code">Текст программы.</param>
    /// <returns>Дерево программы.</returns>
    /// <exception cref="ParseException">Текст записан некорректно.</exception>
    public Statement ParseProgram(string code)
    {
        var parser = new Parser(Lexer.Tokenize(code));
        return parser.ParseProgram();
    }

    /// <summary>
    /// Разбирает текст условия, например постусловие "max > 100".
    /// </summary>
    /// <param name="conditionText">Текст условия.</param>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Текст записан некорректно.</exception>
    public Condition ParseCondition(string conditionText)
    {
        var parser = new Parser(Lexer.Tokenize(conditionText));
        Condition condition = parser.ParseCondition();

        // Условие могло закончиться раньше текста, например "x > 5 )"
        parser.ExpectEnd();
        return condition;
    }
}
