namespace WpEngine.Core.AST;

/// <summary>
/// Базовый узел императивного оператора программы.
/// </summary>
public abstract record Statement;

/// <summary>
/// Оператор присваивания значения переменной: x := expr.
/// При вычислении wp требует применения правила подстановки R[x -> expr]
/// и проверки условия вычислимости выражения.
/// </summary>
public record AssignmentStatement(string Variable, Expr Value) : Statement
{
    public override string ToString() => $"{Variable} := {Value}";
}

/// <summary>
/// Полная форма условного оператора ветвления: if (Condition) { Then } else { Else }.
/// Правило wp: (B && wp(S1, R)) || (!B && wp(S2, R)).
/// </summary>
public record IfStatement(Condition Condition, Statement ThenBranch, Statement ElseBranch) : Statement
{
    public override string ToString() => $"if ({Condition}) {{ {ThenBranch} }} else {{ {ElseBranch} }}";
}

/// <summary>
/// Последовательность выполнения операторов: S1; S2; ...; Sn.
/// Правило wp: разворачивается рекурсивно с конца цепи к началу: wp(S1, wp(S2, R)).
/// </summary>
public record SequenceStatement(List<Statement> Statements) : Statement
{
    public override string ToString() => string.Join("; ", Statements);
}