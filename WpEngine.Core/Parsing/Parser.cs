using System.Globalization;
using WpEngine.Core.AST;

namespace WpEngine.Core.Parsing;

/// <summary>
/// Парсер: собирает из списка токенов дерево AST (методом рекурсивного спуска).
/// </summary>
public class Parser
{
    /// <summary>
    /// Знаки сравнения и соответствующие им операции в AST.
    /// </summary>
    private static readonly Dictionary<TokenType, RelationalOp> RelationalOps = new()
    {
        [TokenType.Equal] = RelationalOp.Equal,
        [TokenType.NotEqual] = RelationalOp.NotEqual,
        [TokenType.Less] = RelationalOp.Less,
        [TokenType.LessOrEqual] = RelationalOp.LessOrEqual,
        [TokenType.Greater] = RelationalOp.Greater,
        [TokenType.GreaterOrEqual] = RelationalOp.GreaterOrEqual
    };

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
    /// Парсит условие: части, соединённые ||.
    /// </summary>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Условие записано некорректно.</exception>
    public Condition ParseCondition()
    {
        Condition left = ParseAnd();

        // Движение вперёд, пока после части стоит ||. Как и в выражениях,
        // прошлый результат каждый раз становится левой частью
        while (Current.Type == TokenType.Or)
        {
            Advance();

            Condition right = ParseAnd();
            left = new LogicalCondition(left, LogicalOp.Or, right);
        }

        return left;
    }

    /// <summary>
    /// Парсит часть условия: части поменьше, соединённые &amp;&amp;.
    /// </summary>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Условие записано некорректно.</exception>
    private Condition ParseAnd()
    {
        Condition left = ParseNot();

        // То же самое, что в ParseCondition, только для &&.
        // Разбирается глубже ||, поэтому "a || b && c" это "a || (b && c)"
        while (Current.Type == TokenType.And)
        {
            Advance();

            Condition right = ParseNot();
            left = new LogicalCondition(left, LogicalOp.And, right);
        }

        return left;
    }

    /// <summary>
    /// Парсит условие со знаком !, если он есть, или обычное условие без него.
    /// </summary>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Условие записано некорректно.</exception>
    private Condition ParseNot()
    {
        // После ! снова вызываем ParseNot, чтобы работало "!!a"
        if (Current.Type == TokenType.Not)
        {
            Advance();
            return new NotCondition(ParseNot());
        }

        return ParseAtom();
    }

    /// <summary>
    /// Парсит самое мелкое условие: true, false, сравнение или условие в скобках.
    /// </summary>
    /// <returns>Дерево условия.</returns>
    /// <exception cref="ParseException">Условие записано некорректно.</exception>
    private Condition ParseAtom()
    {
        // true и false приходят от лексера как обычные имена
        if (Current.Type == TokenType.Identifier && Current.Text is "true" or "false")
        {
            Token literal = Advance();
            return new BooleanLiteralCondition(literal.Text == "true");
        }

        if (Current.Type != TokenType.LeftParen)
            return ParseComparison();

        // Скобка может быть частью выражения, как в "(x + 1) > 5", а может
        // стоять вокруг условия, как в "(x > 5 && y < 3)". Сначала пробуем
        // как сравнение, а если не вышло - возвращаем закладку и читаем как условие
        int start = _pos;

        try
        {
            return ParseComparison();
        }
        catch (ParseException)
        {
            _pos = start;
        }

        Advance();
        Condition inner = ParseCondition();
        Expect(TokenType.RightParen, "Ожидалась закрывающая скобка ')'");
        return inner;
    }

    /// <summary>
    /// Парсит сравнение двух выражений, например x + 1 > 5.
    /// </summary>
    /// <returns>Узел RelationalCondition.</returns>
    /// <exception cref="ParseException">Нет знака сравнения или выражения записаны некорректно.</exception>
    private Condition ParseComparison()
    {
        Expr left = ParseExpression();

        if (!RelationalOps.TryGetValue(Current.Type, out RelationalOp op))
            throw new ParseException(
                $"Ожидался знак сравнения (==, !=, <, <=, >, >=), а найдено {Describe(Current)}",
                Current.Position);

        Advance();

        Expr right = ParseExpression();
        return new RelationalCondition(left, op, right);
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
