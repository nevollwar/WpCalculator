using System.Globalization;
using WpEngine.Core.AST;

namespace WpEngine.Core.Parsing;

/// <summary>
/// Парсер: собирает из списка токенов дерево AST (методом рекурсивного спуска).
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;

    // Индекс текущего токена в списке, разбор начинается с первого
    private int _pos = 0;

    /// <param name="tokens">Токены, полученные от лексера. Последним должен идти End.</param>
    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    /// Текущий токен, который парсер ещё не разобрал.
    /// </summary>
    private Token Current => _tokens[_pos];

    /// <summary>
    /// Парсит арифметическое выражение: слагаемые, соединённые + или -.
    /// </summary>
    /// <returns>Дерево выражения.</returns>
    /// <exception cref="ParseException">Выражение записано некорректно.</exception>
    public Expr ParseExpression()
    {
        Expr left = ParseTerm();

        // Движение вперёд, пока после слагаемого стоит + или -.
        // Каждый новый шаг делает прошлый результат левой частью, поэтому
        // "a - b - c" собирается как "(a - b) - c"
        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            BinaryOp op = Current.Type == TokenType.Plus ? BinaryOp.Add : BinaryOp.Sub;
            Advance();

            Expr right = ParseTerm();
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// Парсит слагаемое: множители, соединённые * или /.
    /// </summary>
    /// <returns>Дерево слагаемого.</returns>
    /// <exception cref="ParseException">Слагаемое записано некорректно.</exception>
    private Expr ParseTerm()
    {
        Expr left = ParseFactor();

        // То же самое, что в ParseExpression, только для "*" и "/".
        // Умножение разбирается глубже сложения, поэтому в дереве оно стоит ниже
        // и считается раньше
        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            BinaryOp op = Current.Type == TokenType.Star ? BinaryOp.Mul : BinaryOp.Div;
            Advance();

            Expr right = ParseFactor();
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// Парсит множитель: число, переменную, abs(...), выражение в скобках
    /// или множитель со знаком минус.
    /// </summary>
    /// <returns>Дерево множителя.</returns>
    /// <exception cref="ParseException">Множитель записан некорректно.</exception>
    private Expr ParseFactor()
    {
        Token token = Current;

        switch (token.Type)
        {
            case TokenType.Number:
                Advance();
                return new NumberExpr(double.Parse(token.Text, CultureInfo.InvariantCulture));

            case TokenType.Identifier:
                // Имя со скобкой после него - вызов функции, иначе обычная переменная
                if (_tokens[_pos + 1].Type == TokenType.LeftParen)
                    return ParseFunctionCall();

                Advance();
                return new VariableExpr(token.Text);

            case TokenType.LeftParen:
                // Скобки в дереве не хранятся, они только меняют порядок разбора
                Advance();
                Expr inner = ParseExpression();
                Expect(TokenType.RightParen, "Ожидалась закрывающая скобка ')'");
                return inner;

            case TokenType.Minus:
                Advance();
                return Negate(ParseFactor());

            default:
                throw new ParseException($"Ожидалось выражение, а найдено {Describe(token)}", token.Position);
        }
    }

    /// <summary>
    /// Парсит вызов функции. Поддерживается только abs(выражение).
    /// </summary>
    /// <returns>Узел AbsExpr.</returns>
    /// <exception cref="ParseException">Функция неизвестна или вызов записан некорректно.</exception>
    private Expr ParseFunctionCall()
    {
        Token name = Advance();

        if (name.Text != "abs")
            throw new ParseException($"Неизвестная функция '{name.Text}', поддерживается только abs", name.Position);

        // Пропуск открывающей скобки, её наличие уже проверено в ParseFactor
        Advance();

        Expr argument = ParseExpression();
        Expect(TokenType.RightParen, "Ожидалась закрывающая скобка ')' после аргумента abs");

        return new AbsExpr(argument);
    }

    /// <summary>
    /// Применяет к выражению унарный минус. В AST для него нет отдельного узла.
    /// </summary>
    /// <param name="expr">Выражение, перед которым стоял минус.</param>
    /// <returns>Число с обратным знаком или разность "0 - выражение".</returns>
    private static Expr Negate(Expr expr)
    {
        // Для числа просто меняем знак, чтобы "-5" не превращалось в "(0 - 5)"
        if (expr is NumberExpr number)
            return new NumberExpr(-number.Value);

        // Для остального выражения заменяем "-x" на "0 - x", тот же результат
        return new BinaryExpr(new NumberExpr(0), BinaryOp.Sub, expr);
    }

    /// <summary>
    /// Возвращает текущий токен и переходит к следующему. На токене End остаётся на месте.
    /// </summary>
    /// <returns>Токен, который был текущим до вызова.</returns>
    private Token Advance()
    {
        Token token = Current;

        if (token.Type != TokenType.End)
            _pos++;

        return token;
    }

    /// <summary>
    /// Проверяет, что текущий токен нужного типа, и переходит к следующему.
    /// </summary>
    /// <param name="type">Ожидаемый тип токена.</param>
    /// <param name="message">Текст ошибки, если тип не совпал.</param>
    /// <returns>Принятый токен.</returns>
    /// <exception cref="ParseException">Текущий токен другого типа.</exception>
    private Token Expect(TokenType type, string message)
    {
        if (Current.Type != type)
            throw new ParseException($"{message}, а найдено {Describe(Current)}", Current.Position);

        return Advance();
    }

    /// <summary>
    /// Описывает токен для сообщения об ошибке.
    /// </summary>
    /// <param name="token">Токен, на котором парсер остановился.</param>
    /// <returns>Текст токена в кавычках или "конец текста".</returns>
    private static string Describe(Token token) =>
        token.Type == TokenType.End ? "конец текста" : $"'{token.Text}'";
}
