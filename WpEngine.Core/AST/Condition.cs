namespace WpEngine.Core.AST;

/// <summary>
/// Базовый узел синтаксического дерева для логических условий (предикатов состояния программы).
/// </summary>
public abstract record Condition
{
    public abstract override string ToString();
}

/// <summary>
/// Операции реляционного сравнения арифметических выражений.
/// </summary>
public enum RelationalOp
{
    Equal,          // ==
    NotEqual,       // !=
    Less,           // <
    LessOrEqual,    // <=
    Greater,        // >
    GreaterOrEqual  // >=
}

/// <summary>
/// Атомарный предикат сравнения двух арифметических выражений (например, x > 100).
/// </summary>
public record RelationalCondition(Expr Left, RelationalOp Op, Expr Right) : Condition
{
    public override string ToString() => Op switch
    {
        RelationalOp.Equal => $"({Left} == {Right})",
        RelationalOp.NotEqual => $"({Left} != {Right})",
        RelationalOp.Less => $"({Left} < {Right})",
        RelationalOp.LessOrEqual => $"({Left} <= {Right})",
        RelationalOp.Greater => $"({Left} > {Right})",
        RelationalOp.GreaterOrEqual => $"({Left} >= {Right})",
        _ => throw new NotImplementedException()
    };
}

/// <summary>
/// Логические связки булевой алгебры.
/// </summary>
public enum LogicalOp
{
    And, // Конъюнкция 
    Or   // Дизъюнкция 
}

/// <summary>
/// Составной предикат, объединяющий два условия логической связкой.
/// </summary>
public record LogicalCondition(Condition Left, LogicalOp Op, Condition Right) : Condition
{
    public override string ToString() => Op switch
    {
        LogicalOp.And => $"({Left} && {Right})",
        LogicalOp.Or => $"({Left} || {Right})",
        _ => throw new NotImplementedException()
    };
}

/// <summary>
/// Предикат логического отрицания (~B, !B).
/// </summary>
public record NotCondition(Condition Inner) : Condition
{
    public override string ToString() => $"!({Inner})";
}

/// <summary>
/// Булев литерал (логическая истина или ложь: true, false).
/// </summary>
public record BooleanLiteralCondition(bool Value) : Condition
{
    public override string ToString() => Value ? "true" : "false";
}