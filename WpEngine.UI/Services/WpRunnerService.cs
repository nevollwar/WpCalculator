using System.Text;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.Core.Parsing;
using WpEngine.Core.Services;

namespace WpEngine.UI.Services;

/// <summary>
/// Сервис пошагового вычисления wp и подготовки LaTeX-представлений для UI.
/// Использует парсер (CodeParser), генератор описаний (Humanizer) и движок подстановки (SubstitutionEngine) из WpEngine.Core.
/// </summary>
public class WpRunnerService : IWpEngine, ICodeParser
{
    private readonly SubstitutionEngine _substitutor = new();
    private readonly ICodeParser _coreParser = new CodeParser();

    /// <summary>
    /// Вычисляет слабейшее предусловие пошагово с конца программы к началу.
    /// </summary>
    public WpResult CalculateWp(Statement program, Condition postCondition)
    {
        var trace = new List<WpStep>();
        var (finalCond, _) = ComputeWpInternal(program, postCondition, trace, stepCounter: 1);

        // Упрощаем итоговое логическое дерево через SubstitutionEngine и алгебраические правила
        var simplified = SimplifyCondition(finalCond);
        simplified = _substitutor.Simplify(simplified);

        // Используем Humanizer из WpEngine.Core.Parsing
        string shortNotation = Humanizer.Format(simplified);
        string humanNotation = Humanizer.Describe(simplified);

        // Формирование триады Хоара {Pre} P {Post} через Humanizer из WpEngine.Core
        string triad = $"{{ {shortNotation} }}\n{Humanizer.Format(program)}\n{{ {Humanizer.Format(postCondition)} }}\n\n{Humanizer.DescribeTriad(simplified, program, postCondition)}";

        return new WpResult(
            simplified,
            shortNotation,
            humanNotation,
            triad,
            trace
        );
    }

    /// <summary>
    /// Алгебраическое и логическое упрощение условий (тождества вида expr == expr => true, линейная свертка).
    /// </summary>
    public Condition SimplifyCondition(Condition cond)
    {
        if (cond == null) return cond!;

        return cond switch
        {
            RelationalCondition rel => SimplifyRelational(rel),
            LogicalCondition log => _substitutor.Simplify(new LogicalCondition(
                SimplifyCondition(log.Left),
                log.Op,
                SimplifyCondition(log.Right)
            )),
            NotCondition not => _substitutor.Simplify(new NotCondition(SimplifyCondition(not.Inner))),
            _ => cond
        };
    }

    private Condition SimplifyRelational(RelationalCondition rel)
    {
        var left = SimplifyExpr(rel.Left);
        var right = SimplifyExpr(rel.Right);

        // Тождество: expr == expr => true
        if (rel.Op == RelationalOp.Equal && left.ToString() == right.ToString())
        {
            return new BooleanLiteralCondition(true);
        }
        // Тождество: expr != expr => false
        if (rel.Op == RelationalOp.NotEqual && left.ToString() == right.ToString())
        {
            return new BooleanLiteralCondition(false);
        }

        // Редукция линейных неравенств вида x + c1 > c2 => x > (c2 - c1)
        if (rel.Op == RelationalOp.Greater &&
            left is BinaryExpr binG && binG.Op == BinaryOp.Add && binG.Left is VariableExpr vG && binG.Right is NumberExpr c1 &&
            right is NumberExpr c2)
        {
            return new RelationalCondition(vG, RelationalOp.Greater, new NumberExpr(c2.Value - c1.Value));
        }

        if (rel.Op == RelationalOp.Less &&
            left is BinaryExpr binL && binL.Op == BinaryOp.Add && binL.Left is VariableExpr vL && binL.Right is NumberExpr c3 &&
            right is NumberExpr c4)
        {
            return new RelationalCondition(vL, RelationalOp.Less, new NumberExpr(c4.Value - c3.Value));
        }

        return new RelationalCondition(left, rel.Op, right);
    }

    private Expr SimplifyExpr(Expr expr)
    {
        if (expr == null) return expr!;

        return expr switch
        {
            // (e + n1) - n2 => e + (n1 - n2)
            BinaryExpr binSub when binSub.Op == BinaryOp.Sub &&
                                   binSub.Left is BinaryExpr binAdd && binAdd.Op == BinaryOp.Add &&
                                   binAdd.Right is NumberExpr n1 && binSub.Right is NumberExpr n2 =>
                CombineAddSub(SimplifyExpr(binAdd.Left), n1.Value - n2.Value),

            // (e - n1) + n2 => e + (n2 - n1)
            BinaryExpr binAdd2 when binAdd2.Op == BinaryOp.Add &&
                                    binAdd2.Left is BinaryExpr binSub2 && binSub2.Op == BinaryOp.Sub &&
                                    binSub2.Right is NumberExpr n3 && binAdd2.Right is NumberExpr n4 =>
                CombineAddSub(SimplifyExpr(binSub2.Left), n4.Value - n3.Value),

            // Вычисление констант: n1 + n2, n1 - n2, n1 * n2, n1 / n2
            BinaryExpr(NumberExpr nA, BinaryOp.Add, NumberExpr nB) => new NumberExpr(nA.Value + nB.Value),
            BinaryExpr(NumberExpr nA, BinaryOp.Sub, NumberExpr nB) => new NumberExpr(nA.Value - nB.Value),
            BinaryExpr(NumberExpr nA, BinaryOp.Mul, NumberExpr nB) => new NumberExpr(nA.Value * nB.Value),
            BinaryExpr(NumberExpr nA, BinaryOp.Div, NumberExpr nB) when nB.Value != 0 => new NumberExpr(nA.Value / nB.Value),

            // Рекурсивный спуск
            BinaryExpr bin => new BinaryExpr(SimplifyExpr(bin.Left), bin.Op, SimplifyExpr(bin.Right)),
            AbsExpr abs => new AbsExpr(SimplifyExpr(abs.Inner)),
            _ => expr
        };
    }

    private static Expr CombineAddSub(Expr e, double diff)
    {
        if (Math.Abs(diff) < 1e-9) return e;
        if (diff > 0) return new BinaryExpr(e, BinaryOp.Add, new NumberExpr(diff));
        return new BinaryExpr(e, BinaryOp.Sub, new NumberExpr(-diff));
    }

    private (Condition Result, List<string> DomainNotes) ComputeWpInternal(
        Statement stmt,
        Condition currentPost,
        List<WpStep> trace,
        int stepCounter)
    {
        var domainNotes = new List<string>();

        switch (stmt)
        {
            case AssignmentStatement assign:
            {
                // Проверка условий определенности (деление на ноль)
                var denominators = ExtractDivisors(assign.Value);
                Condition wp = _substitutor.Substitute(currentPost, assign.Variable, assign.Value);

                var stepNotes = new StringBuilder();
                stepNotes.Append($"Подстановка выражения ({assign.Value}) вместо переменной '{assign.Variable}' в постусловие.");

                if (denominators.Count > 0)
                {
                    foreach (var denom in denominators)
                    {
                        var nonZeroCond = new RelationalCondition(denom, RelationalOp.NotEqual, new NumberExpr(0));
                        wp = new LogicalCondition(nonZeroCond, LogicalOp.And, wp);
                        domainNotes.Add($"Ограничение определенности: ({denom} != 0)");
                        stepNotes.Append($" Добавлено условие существования деления: ({denom} != 0).");
                    }
                }

                wp = _substitutor.Simplify(wp);

                trace.Add(new WpStep(
                    StatementCode: Humanizer.Format(assign),
                    IntermediateCondition: Humanizer.Format(wp),
                    Description: stepNotes.ToString()
                ));

                return (wp, domainNotes);
            }

            case IfStatement ifStmt:
            {
                // Для if: wp(if B S1 else S2, R) = (B && wp(S1, R)) || (!B && wp(S2, R))
                var thenTrace = new List<WpStep>();
                var elseTrace = new List<WpStep>();

                var (wpThen, notesThen) = ComputeWpInternal(ifStmt.ThenBranch, currentPost, thenTrace, stepCounter);
                var (wpElse, notesElse) = ComputeWpInternal(ifStmt.ElseBranch, currentPost, elseTrace, stepCounter);

                domainNotes.AddRange(notesThen);
                domainNotes.AddRange(notesElse);

                // Добавляем шаги веток
                foreach (var st in thenTrace)
                {
                    trace.Add(new WpStep(
                        $"[Ветка Then] {st.StatementCode}",
                        st.IntermediateCondition,
                        st.Description
                    ));
                }

                foreach (var se in elseTrace)
                {
                    trace.Add(new WpStep(
                        $"[Ветка Else] {se.StatementCode}",
                        se.IntermediateCondition,
                        se.Description
                    ));
                }

                Condition condBranchThen = new LogicalCondition(ifStmt.Condition, LogicalOp.And, wpThen);
                Condition condBranchElse = new LogicalCondition(new NotCondition(ifStmt.Condition), LogicalOp.And, wpElse);

                Condition combined = new LogicalCondition(condBranchThen, LogicalOp.Or, condBranchElse);
                combined = _substitutor.Simplify(combined);

                trace.Add(new WpStep(
                    StatementCode: $"if ({Humanizer.Format(ifStmt.Condition)}) ...",
                    IntermediateCondition: Humanizer.Format(combined),
                    Description: "Дизъюнкция по веткам ветвления: (Guard && wp(Then)) || (!Guard && wp(Else))."
                ));

                return (combined, domainNotes);
            }

            case SequenceStatement seq:
            {
                Condition runningCond = currentPost;
                // Двигаемся с конца к началу
                for (int i = seq.Statements.Count - 1; i >= 0; i--)
                {
                    var curStmt = seq.Statements[i];
                    var (nextCond, notes) = ComputeWpInternal(curStmt, runningCond, trace, trace.Count + 1);
                    domainNotes.AddRange(notes);
                    runningCond = nextCond;
                }

                return (runningCond, domainNotes);
            }

            default:
                throw new NotSupportedException($"Неизвестный оператор: {stmt.GetType().Name}");
        }
    }

    /// <summary>
    /// Извлекает знаменатели всех операций деления в выражении для контроля определенности.
    /// </summary>
    private List<Expr> ExtractDivisors(Expr expr)
    {
        var result = new List<Expr>();
        FindDivisorsRecursive(expr, result);
        return result;
    }

    private void FindDivisorsRecursive(Expr expr, List<Expr> result)
    {
        switch (expr)
        {
            case BinaryExpr bin when bin.Op == BinaryOp.Div:
                result.Add(bin.Right);
                FindDivisorsRecursive(bin.Left, result);
                FindDivisorsRecursive(bin.Right, result);
                break;
            case BinaryExpr bin:
                FindDivisorsRecursive(bin.Left, result);
                FindDivisorsRecursive(bin.Right, result);
                break;
            case AbsExpr abs:
                FindDivisorsRecursive(abs.Inner, result);
                break;
        }
    }

    /// <summary>
    /// Форматирует логическое дерево через Humanizer из WpEngine.Core.
    /// </summary>
    public string FormatCondition(Condition cond) => Humanizer.Format(cond);

    /// <summary>
    /// Преобразует предикат в словесное объяснение на русском языке через Humanizer из WpEngine.Core.
    /// </summary>
    public string ToHumanReadable(Condition cond) => Humanizer.Describe(cond);

    /// <summary>
    /// Генерирует математическую нотацию в формате LaTeX для красивого рендеринга и экспорта.
    /// </summary>
    public string ToLatex(Condition cond, bool isRoot = true)
    {
        return cond switch
        {
            RelationalCondition rel => $"{ExprToLatex(rel.Left)} {OpToLatex(rel.Op)} {ExprToLatex(rel.Right)}",
            LogicalCondition log => isRoot
                ? $"{ToLatex(log.Left, false)} {(log.Op == LogicalOp.And ? @"\land" : @"\lor")} {ToLatex(log.Right, false)}"
                : $@"({ToLatex(log.Left, false)} {(log.Op == LogicalOp.And ? @"\land" : @"\lor")} {ToLatex(log.Right, false)})",
            NotCondition not => $@"\neg ({ToLatex(not.Inner, false)})",
            BooleanLiteralCondition lit => lit.Value ? @"\mathbf{true}" : @"\mathbf{false}",
            _ => cond.ToString()
        };
    }

    private static string OpToLatex(RelationalOp op) => op switch
    {
        RelationalOp.Equal => "=",
        RelationalOp.NotEqual => @"\neq",
        RelationalOp.Less => "<",
        RelationalOp.LessOrEqual => @"\le",
        RelationalOp.Greater => ">",
        RelationalOp.GreaterOrEqual => @"\ge",
        _ => "="
    };

    private static string BinOpToString(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+",
        BinaryOp.Sub => "-",
        BinaryOp.Mul => "*",
        BinaryOp.Div => "/",
        _ => "+"
    };

    public string ExprToLatex(Expr expr) => expr switch
    {
        NumberExpr n => n.Value.ToString(),
        VariableExpr v => FormatVarLatex(v.Name),
        BinaryExpr bin when bin.Op == BinaryOp.Div => $@"\frac{{{ExprToLatex(bin.Left)}}}{{{ExprToLatex(bin.Right)}}}",
        BinaryExpr bin when bin.Op == BinaryOp.Mul => $@"{ExprToLatex(bin.Left)} \cdot {ExprToLatex(bin.Right)}",
        BinaryExpr bin => $"{ExprToLatex(bin.Left)} {BinOpToString(bin.Op)} {ExprToLatex(bin.Right)}",
        AbsExpr abs => $@"|{ExprToLatex(abs.Inner)}|",
        _ => expr.ToString()
    };

    private static string FormatVarLatex(string name)
    {
        if (name.Length > 1 && char.IsLetter(name[0]) && char.IsDigit(name[1]))
            return $"{name[0]}_{{{name.Substring(1)}}}";
        if (name.Length > 1)
            return $@"\text{{{name}}}";
        return name;
    }

    public string StatementToLatex(Statement stmt) => stmt switch
    {
        AssignmentStatement assign => $@"{FormatVarLatex(assign.Variable)} := {ExprToLatex(assign.Value)}",
        IfStatement ifStmt => $@"\mathbf{{if}} \; ({ToLatex(ifStmt.Condition, false)}) \; \mathbf{{then}} \; {StatementToLatex(ifStmt.ThenBranch)} \; \mathbf{{else}} \; {StatementToLatex(ifStmt.ElseBranch)}",
        SequenceStatement seq => string.Join(@"; \; ", seq.Statements.Select(StatementToLatex)),
        _ => stmt.ToString()
    };

    public string HoareTriadToLatex(Condition pre, Statement p, Condition post)
    {
        return $@"\left\{{ {ToLatex(pre)} \right\}} \quad {StatementToLatex(p)} \quad \left\{{ {ToLatex(post)} \right\}}";
    }

    /// <summary>
    /// Генерирует форматированную многострочную нотацию LaTeX с переносами строк (\\)
    /// для аккуратного рендеринга формул в карточках интерфейса без горизонтальной обрезки.
    /// </summary>
    public string ToWrappedLatex(Condition cond, bool isRoot = true)
    {
        return cond switch
        {
            RelationalCondition rel => $"{ExprToLatex(rel.Left)} {OpToLatex(rel.Op)} {ExprToLatex(rel.Right)}",
            LogicalCondition log when log.Op == LogicalOp.Or =>
                isRoot
                    ? $"{ToWrappedLatex(log.Left, false)} \\\\ \\lor \\; {ToWrappedLatex(log.Right, false)}"
                    : $"({ToWrappedLatex(log.Left, false)} \\\\ \\lor \\; {ToWrappedLatex(log.Right, false)})",
            LogicalCondition log when log.Op == LogicalOp.And =>
                isRoot
                    ? $"{ToWrappedLatex(log.Left, false)} \\\\ \\land \\; {ToWrappedLatex(log.Right, false)}"
                    : ShouldWrapSubConjunction(log)
                        ? $"({ToWrappedLatex(log.Left, false)} \\\\ \\land \\; {ToWrappedLatex(log.Right, false)})"
                        : $"({ToWrappedLatex(log.Left, false)} \\land {ToWrappedLatex(log.Right, false)})",
            NotCondition not => $@"\neg ({ToWrappedLatex(not.Inner, false)})",
            BooleanLiteralCondition lit => lit.Value ? @"\mathbf{true}" : @"\mathbf{false}",
            _ => cond.ToString()
        };
    }

    private bool ShouldWrapSubConjunction(LogicalCondition log)
    {
        string leftStr = ToLatex(log.Left, false);
        string rightStr = ToLatex(log.Right, false);
        return (leftStr.Length + rightStr.Length) > 28 || leftStr.Contains(@"\frac") || rightStr.Contains(@"\frac");
    }

    /// <summary>
    /// Оборачивает произвольную строку формулы LaTeX переносами \\ при переполнении строки.
    /// </summary>
    public string WrapLatexString(string latex, int threshold = 26)
    {
        if (string.IsNullOrWhiteSpace(latex)) return string.Empty;
        if (latex.Contains(@"\\")) return latex;
        if (latex.Length <= threshold) return latex;

        var orParts = SplitByTopLevel(latex, @"\lor");
        if (orParts.Count > 1)
        {
            return string.Join(@" \\ \lor \; ", orParts.Select(p => WrapLatexString(p, threshold)));
        }

        var andParts = SplitByTopLevel(latex, @"\land");
        if (andParts.Count > 1)
        {
            return string.Join(@" \\ \land \; ", andParts.Select(p => WrapLatexString(p, threshold)));
        }

        return latex;
    }

    private static List<string> SplitByTopLevel(string input, string op)
    {
        var result = new List<string>();
        int depth = 0;
        int lastIdx = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(' || c == '{' || c == '[') depth++;
            else if (c == ')' || c == '}' || c == ']') depth--;
            else if (depth <= 1 && i + op.Length <= input.Length)
            {
                if (string.Compare(input, i, op, 0, op.Length, StringComparison.Ordinal) == 0)
                {
                    result.Add(input.Substring(lastIdx, i - lastIdx).Trim());
                    i += op.Length - 1;
                    lastIdx = i + 1;
                }
            }
        }

        if (lastIdx < input.Length)
        {
            result.Add(input.Substring(lastIdx).Trim());
        }

        return result;
    }

    // =========================================================================
    // ПОДКЛЮЧЕНИЕ ПАРСЕРА WpEngine.Core.Parsing (CodeParser / Lexer / Parser)
    // =========================================================================

    public Statement ParseProgram(string code)
    {
        try
        {
            return _coreParser.ParseProgram(code ?? string.Empty);
        }
        catch (ParseException pex)
        {
            throw ConvertParseException(code ?? string.Empty, pex);
        }
    }

    public Condition ParseCondition(string conditionText)
    {
        try
        {
            return _coreParser.ParseCondition(conditionText ?? string.Empty);
        }
        catch (ParseException pex)
        {
            throw ConvertParseException(conditionText ?? string.Empty, pex);
        }
    }

    /// <summary>
    /// Переводит ParseException из WpEngine.Core.Parsing (с индексом символа Position)
    /// в координаты строки/столбца для подсветки строки с ошибкой в UI.
    /// </summary>
    private static CodeParseException ConvertParseException(string text, ParseException pex)
    {
        int clamped = Math.Clamp(pex.Position, 0, text.Length);
        int line = 1;
        int lineStart = 0;

        for (int i = 0; i < clamped; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        int col = clamped - lineStart + 1;
        int lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = text.Length;
        string snippet = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

        return new CodeParseException(pex.Message, line, col, clamped, snippet);
    }
}

/// <summary>
/// Обёртка над ParseException из WpEngine.Core для привязки к номеру строки и столбцу в редакторе UI.
/// </summary>
public class CodeParseException : FormatException
{
    public int Line { get; }
    public int Column { get; }
    public int Position { get; }
    public string LineSnippet { get; }

    public CodeParseException(string message, int line, int column, int position, string lineSnippet)
        : base(message)
    {
        Line = line;
        Column = column;
        Position = position;
        LineSnippet = lineSnippet;
    }
}
