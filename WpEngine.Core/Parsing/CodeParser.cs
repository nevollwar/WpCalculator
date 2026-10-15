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
        List<Token> tokens = Lexer.Tokenize(code);

        // В списке только токен End: текст пустой или состоит из одних пробелов
        if (tokens.Count == 1)
            throw new ParseException("Программа пуста", 0);

        return new Parser(tokens).ParseProgram();
    }

    /// <summary>
    /// Разбирает текст условия, например постусловие "max > 100".
    /// </summary>
    /// <param name="conditionText">Текст условия.</param>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Текст записан некорректно.</exception>
    public Condition ParseCondition(string conditionText)
    {
        List<Token> tokens = Lexer.Tokenize(conditionText);

        // В списке только токен End: текст пустой или состоит из одних пробелов
        if (tokens.Count == 1)
            throw new ParseException("Условие пустое", 0);

        var parser = new Parser(tokens);
        Condition condition = parser.ParseCondition();

        // Условие могло закончиться раньше текста, например "x > 5 )"
        parser.ExpectEnd();
        return condition;
    }
}
