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
