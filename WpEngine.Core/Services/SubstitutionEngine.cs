using WpEngine.Core.AST;

namespace WpEngine.Core.Services;

/// <summary>
/// Сервис синтаксической подстановки и редукции предикатов.
/// Реализует базовую аксиому Дейкстры для оператора присваивания: wp(x := e, R) = R[x -> e],
/// заменяя все свободные вхождения переменной x в постусловии R на выражение e.
/// </summary>
public class SubstitutionEngine
{
    /// <summary>
    /// Выполняет подстановку выражения вместо заданной переменной в предикате (логическом условии).
    /// </summary>
    /// <param name="condition">Исходный предикат R.</param>
    /// <param name="targetVariable">Имя замещаемой переменной x.</param>
    /// <param name="replacement">Арифметическое выражение e, подставляемое вместо переменной.</param>
    /// <returns>Новый предикат R[x -> e] с выполненной подстановкой.</returns>
    public Condition Substitute(Condition condition, string targetVariable, Expr replacement)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetVariable);
        ArgumentNullException.ThrowIfNull(replacement);

        return condition switch
        {
            // Для отношений (>, <, == и др.) подстановка спускается в левое и правое выражения
            RelationalCondition rel => new RelationalCondition(
                SubstituteExpr(rel.Left, targetVariable, replacement),
                rel.Op,
                SubstituteExpr(rel.Right, targetVariable, replacement)
            ),

            // Для логических связок (И, ИЛИ) рекурсивно обрабатываем обе ветви
            LogicalCondition log => new LogicalCondition(
                Substitute(log.Left, targetVariable, replacement),
                log.Op,
                Substitute(log.Right, targetVariable, replacement)
            ),

            // Для отрицания спускаемся во внутреннее условие
            NotCondition not => new NotCondition(
                Substitute(not.Inner, targetVariable, replacement)
            ),

            // Булевы константы (true, false) не содержат переменных и остаются неизменными
            BooleanLiteralCondition lit => lit,

            _ => throw new NotSupportedException($"Неподдерживаемый тип предиката: {condition.GetType().Name}")
        };
    }

    /// <summary>
    /// Выполняет синтаксическую замену переменной на выражение в дереве арифметических выражений.
    /// </summary>
    /// <param name="expression">Исходное выражение.</param>
    /// <param name="targetVariable">Имя заменяемой переменной.</param>
    /// <param name="replacement">Выражение для подстановки.</param>
    /// <returns>Результирующее арифметическое выражение.</returns>
    public Expr SubstituteExpr(Expr expression, string targetVariable, Expr replacement)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            // Если идентификатор совпадает с искомой переменной — заменяем
            VariableExpr v when v.Name == targetVariable => replacement,

            // Остальные переменные оставляем без изменений
            VariableExpr v => v,

            // Числовые литералы остаются без изменений
            NumberExpr n => n,

            // Рекурсивный спуск в аргумент функции модуля
            AbsExpr abs => new AbsExpr(SubstituteExpr(abs.Inner, targetVariable, replacement)),

            // Рекурсивный спуск в бинарные операции (+, -, *, /)
            BinaryExpr bin => new BinaryExpr(
                SubstituteExpr(bin.Left, targetVariable, replacement),
                bin.Op,
                SubstituteExpr(bin.Right, targetVariable, replacement)
            ),

            _ => throw new NotSupportedException($"Неподдерживаемый тип выражения: {expression.GetType().Name}")
        };
    }

    /// <summary>
    /// Выполняет базовую логическую редукцию предиката на основе законов булевой алгебры:
    /// устранение двойного отрицания (!(!A) => A), свертка нейтральных элементов (A && true => A).
    /// </summary>
    /// <param name="condition">Исходный предикат.</param>
    /// <returns>Упрощенный предикат.</returns>
    public Condition Simplify(Condition condition)
    {
        if (condition is null) return condition!;

        return condition switch
        {
            // Закон двойного отрицания: !(!A) <=> A
            NotCondition(NotCondition inner) => Simplify(inner.Inner),

            // Законы конъюнкции: A && true <=> A, true && A <=> A
            LogicalCondition(Condition left, LogicalOp.And, BooleanLiteralCondition(true)) => Simplify(left),
            LogicalCondition(BooleanLiteralCondition(true), LogicalOp.And, Condition right) => Simplify(right),

            // Законы аннулирования конъюнкции: A && false <=> false
            LogicalCondition(_, LogicalOp.And, BooleanLiteralCondition(false)) => new BooleanLiteralCondition(false),
            LogicalCondition(BooleanLiteralCondition(false), LogicalOp.And, _) => new BooleanLiteralCondition(false),

            // Законы дизъюнкции: A || false <=> A, false || A <=> A
            LogicalCondition(Condition left, LogicalOp.Or, BooleanLiteralCondition(false)) => Simplify(left),
            LogicalCondition(BooleanLiteralCondition(false), LogicalOp.Or, Condition right) => Simplify(right),

            // Законы аннулирования дизъюнкции: A || true <=> true
            LogicalCondition(_, LogicalOp.Or, BooleanLiteralCondition(true)) => new BooleanLiteralCondition(true),
            LogicalCondition(BooleanLiteralCondition(true), LogicalOp.Or, _) => new BooleanLiteralCondition(true),

            // Рекурсивный спуск для составных условий
            LogicalCondition log => new LogicalCondition(Simplify(log.Left), log.Op, Simplify(log.Right)),
            NotCondition not => new NotCondition(Simplify(not.Inner)),

            _ => condition
        };
    }
}