using System.Globalization;
using WpEngine.Core.AST;

namespace WpEngine.Core.Parsing;

/// <summary>
/// Humanizer: переводит условия в понятный русский текст.
/// </summary>
public static class Humanizer
{
    /// <summary>
    /// Описывает условие словами, например "x больше 5 и y равен 4".
    /// </summary>
    /// <param name="condition">Условие, которое нужно описать.</param>
    /// <returns>Текст на русском языке.</returns>
    /// <exception cref="ArgumentException">Неизвестный тип условия.</exception>
    public static string Describe(Condition condition) => condition switch
    {
        RelationalCondition relational => DescribeRelational(relational),
        LogicalCondition logical => DescribeLogical(logical),
        NotCondition not => DescribeNot(not),
        BooleanLiteralCondition literal => literal.Value ? "истина" : "ложь",
        _ => throw new ArgumentException($"Неизвестный тип условия: {condition.GetType().Name}")
    };

    /// <summary>
    /// Описывает триаду Хоара {Pre} P {Post} словами.
    /// </summary>
    /// <param name="pre">Предусловие.</param>
    /// <param name="program">Программа.</param>
    /// <param name="post">Постусловие.</param>
    /// <returns>Текст вида "Если перед выполнением программы "P" верно: ..., то после её выполнения будет верно: ...".</returns>
    public static string DescribeTriad(Condition pre, Statement program, Condition post) =>
        $"Если перед выполнением программы \"{Format(program)}\" верно: {Describe(pre)}, " +
        $"то после её выполнения будет верно: {Describe(post)}.";

    /// <summary>
    /// Записывает условие краткой нотацией, например "x > 5 && y == 4".
    /// Скобки ставит только там, где они нужны для порядка действий.
    /// </summary>
    /// <param name="condition">Условие.</param>
    /// <returns>Краткая запись, которую можно снова разобрать парсером.</returns>
    /// <exception cref="ArgumentException">Неизвестный тип условия.</exception>
    public static string Format(Condition condition) => condition switch
    {
        RelationalCondition relational =>
            $"{FormatExpr(relational.Left)} {RelationalSymbol(relational.Op)} {FormatExpr(relational.Right)}",
        LogicalCondition logical => FormatLogical(logical),
        NotCondition not => not.Inner is BooleanLiteralCondition
            ? $"!{Format(not.Inner)}"
            : $"!({Format(not.Inner)})",
        BooleanLiteralCondition literal => literal.Value ? "true" : "false",
        _ => throw new ArgumentException($"Неизвестный тип условия: {condition.GetType().Name}")
    };

    /// <summary>
    /// Записывает программу в одну строку, в том же виде, в котором её вводят.
    /// </summary>
    /// <param name="statement">Программа или её часть.</param>
    /// <returns>Строка вида "x := x + 10; y := x + 1".</returns>
    /// <exception cref="ArgumentException">Неизвестный тип оператора.</exception>
    public static string Format(Statement statement) => statement switch
    {
        AssignmentStatement assignment => $"{assignment.Variable} := {FormatExpr(assignment.Value)}",
        SequenceStatement sequence => string.Join("; ", sequence.Statements.Select(s => Format(s))),
        IfStatement ifStatement =>
            $"if ({Format(ifStatement.Condition)}) {{ {Format(ifStatement.ThenBranch)} }} " +
            $"else {{ {Format(ifStatement.ElseBranch)} }}",
        _ => throw new ArgumentException($"Неизвестный тип оператора: {statement.GetType().Name}")
    };

    /// <summary>
    /// Записывает два условия, соединённые "&amp;&amp;" или "||".
    /// </summary>
    /// <param name="condition">Составное условие.</param>
    /// <returns>Строка вида "A &amp;&amp; B".</returns>
    private static string FormatLogical(LogicalCondition condition)
    {
        string symbol = condition.Op == LogicalOp.And ? "&&" : "||";
        int priority = LogicalPriority(condition);

        string left = Format(condition.Left);
        string right = Format(condition.Right);

        // Слева скобки нужны, если там связка слабее: "(a || b) && c"
        if (LogicalPriority(condition.Left) < priority)
            left = $"({left})";

        // Справа скобки нужны и при равном приоритете, как и в арифметике
        if (LogicalPriority(condition.Right) <= priority)
            right = $"({right})";

        return $"{left} {symbol} {right}";
    }

    /// <summary>
    /// Определяет приоритет условия: чем больше число, тем раньше оно разбирается.
    /// </summary>
    /// <param name="condition">Условие.</param>
    /// <returns>1 для "||", 2 для "&amp;&amp;", 3 для всего остального.</returns>
    private static int LogicalPriority(Condition condition) => condition switch
    {
        LogicalCondition { Op: LogicalOp.Or } => 1,
        LogicalCondition => 2,
        _ => 3
    };

    /// <summary>
    /// Возвращает знак сравнения в краткой нотации.
    /// </summary>
    /// <param name="op">Операция сравнения.</param>
    /// <returns>Один из знаков: ==, !=, &lt;, &lt;=, &gt;, &gt;=.</returns>
    private static string RelationalSymbol(RelationalOp op) => op switch
    {
        RelationalOp.Equal => "==",
        RelationalOp.NotEqual => "!=",
        RelationalOp.Less => "<",
        RelationalOp.LessOrEqual => "<=",
        RelationalOp.Greater => ">",
        RelationalOp.GreaterOrEqual => ">=",
        _ => throw new ArgumentException($"Неизвестная операция сравнения: {op}")
    };

    /// <summary>
    /// Описывает сравнение двух выражений.
    /// </summary>
    /// <param name="condition">Сравнение, например x + 1 &gt; 5.</param>
    /// <returns>Текст вида "x + 1 больше 5".</returns>
    private static string DescribeRelational(RelationalCondition condition)
    {
        // Для переменной говорим "равен", для выражения "равно": "x равен 4", "x + 1 равно 5"
        bool isVariable = condition.Left is VariableExpr;

        string verb = condition.Op switch
        {
            RelationalOp.Equal => isVariable ? "равен" : "равно",
            RelationalOp.NotEqual => isVariable ? "не равен" : "не равно",
            RelationalOp.Less => "меньше",
            RelationalOp.LessOrEqual => "меньше или равно",
            RelationalOp.Greater => "больше",
            RelationalOp.GreaterOrEqual => "больше или равно",
            _ => throw new ArgumentException($"Неизвестная операция сравнения: {condition.Op}")
        };

        return $"{FormatExpr(condition.Left)} {verb} {FormatExpr(condition.Right)}";
    }

    /// <summary>
    /// Описывает два условия, соединённые "и" или "или".
    /// </summary>
    /// <param name="condition">Составное условие.</param>
    /// <returns>Текст вида "A и B" или "A или B".</returns>
    private static string DescribeLogical(LogicalCondition condition)
    {
        string left = DescribeOperand(condition.Left, condition.Op);
        string right = DescribeOperand(condition.Right, condition.Op);
        string word = condition.Op == LogicalOp.And ? "и" : "или";

        return $"{left} {word} {right}";
    }

    /// <summary>
    /// Описывает одну из частей составного условия.
    /// </summary>
    /// <param name="operand">Часть условия.</param>
    /// <param name="parentOp">Связка, внутри которой стоит эта часть.</param>
    /// <returns>Текст части, в скобках, если внутри другая связка.</returns>
    private static string DescribeOperand(Condition operand, LogicalOp parentOp)
    {
        string text = Describe(operand);

        // "и" внутри "или" (и наоборот) берём в скобки, иначе фраза читается двояко
        if (operand is LogicalCondition logical && logical.Op != parentOp)
            return $"({text})";

        return text;
    }

    /// <summary>
    /// Описывает отрицание условия.
    /// </summary>
    /// <param name="condition">Условие со знаком "!".</param>
    /// <returns>Текст вида "неверно, что ...".</returns>
    private static string DescribeNot(NotCondition condition)
    {
        string text = Describe(condition.Inner);

        // Составное условие после "неверно, что" берём в скобки
        if (condition.Inner is LogicalCondition)
            text = $"({text})";

        return $"неверно, что {text}";
    }

    /// <summary>
    /// Записывает арифметическое выражение математической нотацией, скобки
    /// ставит только там, где они нужны для порядка действий.
    /// </summary>
    /// <param name="expr">Выражение.</param>
    /// <returns>Строка вида "(x + 1) * 2".</returns>
    private static string FormatExpr(Expr expr)
    {
        switch (expr)
        {
            case NumberExpr number:
                // InvariantCulture, чтобы дробная часть всегда была через точку
                return number.Value.ToString(CultureInfo.InvariantCulture);

            case VariableExpr variable:
                return variable.Name;

            case AbsExpr abs:
                return $"abs({FormatExpr(abs.Inner)})";

            case BinaryExpr binary:
                return FormatBinary(binary);

            default:
                throw new ArgumentException($"Неизвестный тип выражения: {expr.GetType().Name}");
        }
    }

    /// <summary>
    /// Записывает бинарную операцию вместе с подвыражениями.
    /// </summary>
    /// <param name="binary">Бинарное выражение.</param>
    /// <returns>Строка вида "a + b".</returns>
    private static string FormatBinary(BinaryExpr binary)
    {
        string symbol = binary.Op switch
        {
            BinaryOp.Add => "+",
            BinaryOp.Sub => "-",
            BinaryOp.Mul => "*",
            BinaryOp.Div => "/",
            _ => throw new ArgumentException($"Неизвестная операция: {binary.Op}")
        };

        int priority = Priority(binary);
        string left = FormatExpr(binary.Left);
        string right = FormatExpr(binary.Right);

        // Слева скобки нужны, если там действие слабее: "(a + b) * c"
        if (Priority(binary.Left) < priority)
            left = $"({left})";

        // Справа скобки нужны и при равном приоритете: "a - (b - c)" это не "a - b - c".
        // Отрицательное число тоже берём в скобки, чтобы не вышло "a - -5"
        if (Priority(binary.Right) <= priority || binary.Right is NumberExpr { Value: < 0 })
            right = $"({right})";

        return $"{left} {symbol} {right}";
    }

    /// <summary>
    /// Определяет приоритет выражения: чем больше число, тем раньше выполняется действие.
    /// </summary>
    /// <param name="expr">Выражение.</param>
    /// <returns>1 для "+" и "-", 2 для "*" и "/", 3 для чисел, переменных и abs.</returns>
    private static int Priority(Expr expr) => expr switch
    {
        BinaryExpr { Op: BinaryOp.Add or BinaryOp.Sub } => 1,
        BinaryExpr => 2,
        _ => 3
    };
}
