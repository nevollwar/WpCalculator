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
    /// Парсит программу целиком: операторы, разделённые ";".
    /// </summary>
    /// <returns>Один оператор, если он единственный, иначе SequenceStatement.</returns>
    /// <exception cref="ParseException">Программа записана некорректно.</exception>
    public Statement ParseProgram()
    {
        Statement program = ParseStatementList();

        // Список мог закончиться раньше конца текста, например на лишней "}"
        ExpectEnd();
        return program;
    }

    /// <summary>
    /// Проверяет, что весь текст разобран и дальше ничего нет.
    /// </summary>
    /// <exception cref="ParseException">После разобранной части остался лишний текст.</exception>
    public void ExpectEnd()
    {
        Expect(TokenType.End, "Ожидался конец текста");
    }

    /// <summary>
    /// Парсит список операторов, разделённых ";". Заканчивается на "}" или на конце текста.
    /// После последнего оператора ";" можно не ставить, а после if-else можно не ставить нигде.
    /// </summary>
    /// <returns>Один оператор, если он единственный, иначе SequenceStatement.</returns>
    /// <exception cref="ParseException">Оператор записан некорректно или пропущена ";".</exception>
    private Statement ParseStatementList()
    {
        var statements = new List<Statement>();

        while (true)
        {
            Statement statement = ParseStatement();
            statements.Add(statement);

            // Точка с запятой после оператора необязательна, но если есть - пропускаем
            bool hasSemicolon = Current.Type == TokenType.Semicolon;
            if (hasSemicolon)
                Advance();

            // Список закончился
            if (Current.Type is TokenType.End or TokenType.RightBrace)
                break;

            // Дальше идёт ещё оператор, а разделителя нет. Для if-else это допустимо,
            // потому что его и так закрывает "}"
            if (!hasSemicolon && statement is not IfStatement)
                throw new ParseException(
                    $"Ожидалась ';' между операторами, а найдено {Describe(Current)}",
                    Current.Position);
        }

        return statements.Count == 1 ? statements[0] : new SequenceStatement(statements);
    }

    /// <summary>
    /// Парсит один оператор: присваивание или ветвление if-else.
    /// </summary>
    /// <returns>AssignmentStatement или IfStatement.</returns>
    /// <exception cref="ParseException">Оператор записан некорректно.</exception>
    private Statement ParseStatement()
    {
        if (Current.Type != TokenType.Identifier)
            throw new ParseException($"Ожидался оператор, а найдено {Describe(Current)}", Current.Position);

        if (Current.Text == "if")
            return ParseIf();

        // else без своего if
        if (Current.Text == "else")
            throw new ParseException("Найдено else без соответствующего if", Current.Position);

        return ParseAssignment();
    }

    /// <summary>
    /// Парсит присваивание: имя, ":=" и выражение.
    /// </summary>
    /// <returns>Узел AssignmentStatement.</returns>
    /// <exception cref="ParseException">Присваивание записано некорректно.</exception>
    private Statement ParseAssignment()
    {
        Token name = Advance();
        Expect(TokenType.Assign, $"Ожидалось ':=' после '{name.Text}'");

        Expr value = ParseExpression();
        return new AssignmentStatement(name.Text, value);
    }

    /// <summary>
    /// Парсит ветвление: if (условие) { ... } else { ... }. Ветка else обязательна.
    /// </summary>
    /// <returns>Узел IfStatement.</returns>
    /// <exception cref="ParseException">Ветвление записано некорректно.</exception>
    private Statement ParseIf()
    {
        // Пропуск слова if
        Advance();

        Expect(TokenType.LeftParen, "Ожидалась '(' после if");
        Condition condition = ParseCondition();
        Expect(TokenType.RightParen, "Ожидалась ')' после условия");

        Statement thenBranch = ParseBlock();

        // Поддерживается только полная форма, поэтому else обязателен
        if (Current.Type != TokenType.Identifier || Current.Text != "else")
            throw new ParseException(
                $"Ожидалось else (нужна полная форма if-else), а найдено {Describe(Current)}",
                Current.Position);

        Advance();

        Statement elseBranch = ParseBlock();
        return new IfStatement(condition, thenBranch, elseBranch);
    }

    /// <summary>
    /// Парсит блок операторов в фигурных скобках: { ... }.
    /// </summary>
    /// <returns>Один оператор, если он единственный, иначе SequenceStatement.</returns>
    /// <exception cref="ParseException">Блок пустой или записан некорректно.</exception>
    private Statement ParseBlock()
    {
        Expect(TokenType.LeftBrace, "Ожидалась '{'");
        Statement body = ParseStatementList();
        Expect(TokenType.RightBrace, "Ожидалась '}'");

        return body;
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
