namespace WpEngine.Core.AST;

/// <summary>
/// Базовый абстрактный узел синтаксического дерева для арифметических выражений.
/// </summary>
public abstract record Expr
{
    public abstract override string ToString();
}

/// <summary>
/// Числовая константа в выражении.
/// </summary>
/// <param name="Value">Вещественное значение константы.</param>
public record NumberExpr(double Value) : Expr
{
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Переменная состояния программы (идентификатор).
/// </summary>
/// <param name="Name">Имя переменной (например, x, max, D).</param>
public record VariableExpr(string Name) : Expr
{
    public override string ToString() => Name;
}

/// <summary>
/// Поддерживаемые бинарные арифметические операции.
/// </summary>
public enum BinaryOp
{
    Add, // Сложение (+)
    Sub, // Вычитание (-)
    Mul, // Умножение (*)
    Div  // Деление (/)
}

/// <summary>
/// Бинарная арифметическая операция над двумя подвыражениями.
/// </summary>
public record BinaryExpr(Expr Left, BinaryOp Op, Expr Right) : Expr
{
    public override string ToString() => Op switch
    {
        BinaryOp.Add => $"({Left} + {Right})",
        BinaryOp.Sub => $"({Left} - {Right})",
        BinaryOp.Mul => $"({Left} * {Right})",
        BinaryOp.Div => $"({Left} / {Right})",
        _ => throw new NotImplementedException()
    };
}

/// <summary>
/// Унарная функция вычисления абсолютного значения: abs(expr).
/// </summary>
public record AbsExpr(Expr Inner) : Expr
{
    public override string ToString() => $"abs({Inner})";
}