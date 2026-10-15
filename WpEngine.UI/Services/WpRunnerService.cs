using System.Text;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.Core.Services;

namespace WpEngine.UI.Services;

/// <summary>
/// Сервис синтаксического анализа и пошагового вычисления wp.
/// Реализует контракты IWpEngine и ICodeParser для полной автономности UI.
/// </summary>
public class WpRunnerService : IWpEngine, ICodeParser
{
    private readonly SubstitutionEngine _substitutor = new();

    /// <summary>
    /// Вычисляет слабейшее предусловие пошагово с конца программы к началу.
    /// </summary>
    public WpResult CalculateWp(Statement program, Condition postCondition)
    {
        var trace = new List<WpStep>();
        var (finalCond, domainNotes) = ComputeWpInternal(program, postCondition, trace, stepCounter: 1);

        // Упрощаем итоговое логическое дерево
        var simplified = SimplifyCondition(finalCond);
        simplified = _substitutor.Simplify(simplified);
        string shortNotation = FormatCondition(simplified);
        string humanNotation = ToHumanReadable(simplified);

        // Формирование триады Хоара {Pre} P {Post}
        string triad = $"{{ {shortNotation} }}\n{program}\n{{ {FormatCondition(postCondition)} }}";

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
            NotCondition not => new NotCondition(SimplifyCondition(not.Inner)),
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
                    StatementCode: assign.ToString(),
                    IntermediateCondition: FormatCondition(wp),
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
                    StatementCode: $"if ({ifStmt.Condition}) ...",
                    IntermediateCondition: FormatCondition(combined),
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
    /// Форматирует логическое дерево в читаемую математическую нотацию.
    /// </summary>
    public string FormatCondition(Condition cond)
    {
        return FormatCondition(cond, isRoot: true);
    }

    private string FormatCondition(Condition cond, bool isRoot)
    {
        return cond switch
        {
            RelationalCondition rel => $"{FormatExpr(rel.Left)} {OpToString(rel.Op)} {FormatExpr(rel.Right)}",
            LogicalCondition log => isRoot
                ? $"{FormatCondition(log.Left, false)} {(log.Op == LogicalOp.And ? "∧" : "∨")} {FormatCondition(log.Right, false)}"
                : $"({FormatCondition(log.Left, false)} {(log.Op == LogicalOp.And ? "∧" : "∨")} {FormatCondition(log.Right, false)})",
            NotCondition not => $"¬({FormatCondition(not.Inner, false)})",
            BooleanLiteralCondition lit => lit.Value ? "true" : "false",
            _ => cond.ToString()
        };
    }

    private static string OpToString(RelationalOp op) => op switch
    {
        RelationalOp.Equal => "=",
        RelationalOp.NotEqual => "≠",
        RelationalOp.Less => "<",
        RelationalOp.LessOrEqual => "≤",
        RelationalOp.Greater => ">",
        RelationalOp.GreaterOrEqual => "≥",
        _ => "="
    };

    private static string FormatExpr(Expr expr) => expr switch
    {
        NumberExpr n => n.Value.ToString(),
        VariableExpr v => v.Name,
        BinaryExpr bin => $"{FormatExpr(bin.Left)} {BinOpToString(bin.Op)} {FormatExpr(bin.Right)}",
        AbsExpr abs => $"abs({FormatExpr(abs.Inner)})",
        _ => expr.ToString()
    };

    private static string BinOpToString(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+",
        BinaryOp.Sub => "-",
        BinaryOp.Mul => "*",
        BinaryOp.Div => "/",
        _ => "+"
    };

    /// <summary>
    /// Преобразует предикат в словесное объяснение на человеческом языке (для отчета).
    /// </summary>
    public string ToHumanReadable(Condition cond)
    {
        return cond switch
        {
            RelationalCondition rel => $"{rel.Left} {OpToHuman(rel.Op)} {rel.Right}",
            LogicalCondition log => $"({ToHumanReadable(log.Left)} {(log.Op == LogicalOp.And ? "И" : "ИЛИ")} {ToHumanReadable(log.Right)})",
            NotCondition not => $"НЕ({ToHumanReadable(not.Inner)})",
            BooleanLiteralCondition lit => lit.Value ? "Истина (всегда выполнено)" : "Ложь (недостижимо)",
            _ => cond.ToString()
        };
    }

    private static string OpToHuman(RelationalOp op) => op switch
    {
        RelationalOp.Equal => "равно",
        RelationalOp.NotEqual => "не равно",
        RelationalOp.Less => "меньше",
        RelationalOp.LessOrEqual => "меньше или равно",
        RelationalOp.Greater => "больше",
        RelationalOp.GreaterOrEqual => "больше или равно",
        _ => "="
    };

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

    // ==========================================
    // ПАРСЕР ПОЛЬЗОВАТЕЛЬСКОГО ВВОДА
    // ==========================================

    public Statement ParseProgram(string code)
    {
        var parser = new TokenParser(code);
        return parser.ParseProgram();
    }

    public Condition ParseCondition(string conditionText)
    {
        var parser = new TokenParser(conditionText);
        return parser.ParseFullCondition();
    }
}

/// <summary>
/// Исключение синтаксического анализа с точной привязкой к номеру строки и столбцу.
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

/// <summary>
/// Лексер и рекурсивный синтаксический анализатор для операторов и предикатов с отслеживанием строк.
/// </summary>
internal class TokenParser
{
    private readonly string _text;
    private int _pos;
    private int _lastTokenEndPos;

    public TokenParser(string text)
    {
        _text = text ?? string.Empty;
        _pos = 0;
        _lastTokenEndPos = 0;
    }

    private CodeParseException CreateError(int pos, string message)
    {
        int clamped = Math.Clamp(pos, 0, _text.Length);
        int line = 1;
        int lineStart = 0;

        for (int i = 0; i < clamped; i++)
        {
            if (_text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        int col = clamped - lineStart + 1;
        int lineEnd = _text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = _text.Length;
        string snippet = _text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

        return new CodeParseException(message, line, col, clamped, snippet);
    }

    private void ValidateBrackets()
    {
        var stack = new Stack<(char Ch, int Pos)>();
        for (int i = 0; i < _text.Length; i++)
        {
            char c = _text[i];
            if (c == '(' || c == '{')
            {
                stack.Push((c, i));
            }
            else if (c == ')' || c == '}')
            {
                char expectedOpen = c == ')' ? '(' : '{';
                if (stack.Count == 0)
                {
                    throw CreateError(i, $"Лишняя закрывающая скобка '{c}' без парной открывающей.");
                }
                var top = stack.Pop();
                if (top.Ch != expectedOpen)
                {
                    throw CreateError(i, $"Несовпадение скобок: ожидалась закрывающая для '{top.Ch}', но встречена '{c}'.");
                }
            }
        }

        if (stack.Count > 0)
        {
            var unclosed = stack.Pop();
            char expectedClose = unclosed.Ch == '(' ? ')' : '}';
            throw CreateError(unclosed.Pos, $"Не закрыта скобка '{unclosed.Ch}' (ожидалась '{expectedClose}').");
        }
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            _pos++;
    }

    private bool HasNewlineBetween(int fromPos, int toPos)
    {
        int start = Math.Clamp(fromPos, 0, _text.Length);
        int end = Math.Clamp(toPos, 0, _text.Length);
        for (int i = start; i < end; i++)
        {
            if (_text[i] == '\n') return true;
        }
        return false;
    }

    private bool Peek(string s)
    {
        SkipWhitespace();
        if (_pos + s.Length > _text.Length) return false;
        return string.Compare(_text, _pos, s, 0, s.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private bool PeekWord(string word)
    {
        SkipWhitespace();
        if (_pos + word.Length > _text.Length) return false;
        if (string.Compare(_text, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return false;
        int after = _pos + word.Length;
        return after >= _text.Length || (!char.IsLetterOrDigit(_text[after]) && _text[after] != '_');
    }

    private bool Match(string s)
    {
        if (Peek(s))
        {
            _pos += s.Length;
            _lastTokenEndPos = _pos;
            return true;
        }
        return false;
    }

    private bool MatchWord(string word)
    {
        if (PeekWord(word))
        {
            _pos += word.Length;
            _lastTokenEndPos = _pos;
            return true;
        }
        return false;
    }

    public Statement ParseProgram()
    {
        ValidateBrackets();

        var list = new List<Statement>();
        SkipWhitespace();

        while (_pos < _text.Length)
        {
            if (Match(";")) continue;
            var stmt = ParseStatement();
            if (stmt != null)
                list.Add(stmt);
            SkipWhitespace();
        }

        if (list.Count == 0)
            throw CreateError(0, "Программа не содержит операторов.");

        return list.Count == 1 ? list[0] : new SequenceStatement(list);
    }

    private Statement ParseStatement()
    {
        SkipWhitespace();
        if (PeekWord("if"))
        {
            int ifPos = _pos;
            MatchWord("if");
            SkipWhitespace();
            bool hasParen = Match("(");
            if (!hasParen)
                throw CreateError(_lastTokenEndPos, "После ключевого слова 'if' ожидалось условие в круглых скобках '('.");

            var cond = ParseCondition();
            if (!Match(")"))
                throw CreateError(_pos, "Ожидалась закрывающая скобка ')' после условия if.");

            Statement thenBranch = ParseBlockOrSingle();
            SkipWhitespace();

            Statement elseBranch;
            if (MatchWord("else"))
            {
                elseBranch = ParseBlockOrSingle();
            }
            else
            {
                elseBranch = new SequenceStatement(new List<Statement>());
            }

            return new IfStatement(cond, thenBranch, elseBranch);
        }

        if (PeekWord("else"))
        {
            throw CreateError(_pos, "Ключевое слово 'else' без предшествующего блока 'if'.");
        }

        // Присваивание: x := expr;
        int varStartPos = _pos;
        string varName = ParseIdentifier();
        int afterVarPos = _lastTokenEndPos;
        SkipWhitespace();

        if (Match(":=") || Match("≔") || Match("="))
        {
            var expr = ParseExpr();
            SkipWhitespace();

            // Если точка с запятой пропущена, но на этой же строке сразу идет еще один токен (не '}' и не конец строки)
            if (!Match(";"))
            {
                if (_pos < _text.Length && _text[_pos] != '}' && !HasNewlineBetween(_lastTokenEndPos, _pos))
                {
                    throw CreateError(_pos, $"Неожиданный токен '{GetTokenPreview(_pos)}' — возможно, пропущена точка с запятой ';' или арифметический оператор.");
                }
            }
            return new AssignmentStatement(varName, expr);
        }

        throw CreateError(afterVarPos, $"После переменной '{varName}' ожидался оператор присваивания ':='.");
    }

    private string GetTokenPreview(int pos)
    {
        if (pos >= _text.Length) return "конец строки";
        int end = pos;
        while (end < _text.Length && !char.IsWhiteSpace(_text[end]) && end - pos < 12)
            end++;
        return _text.Substring(pos, Math.Max(1, end - pos));
    }

    private Statement ParseBlockOrSingle()
    {
        SkipWhitespace();
        if (Match("{"))
        {
            int openBracePos = _lastTokenEndPos - 1;
            var stmts = new List<Statement>();
            SkipWhitespace();
            while (_pos < _text.Length && !Peek("}"))
            {
                if (Match(";")) continue;
                stmts.Add(ParseStatement());
                SkipWhitespace();
            }
            if (!Match("}"))
                throw CreateError(openBracePos, "Не закрыта фигурная скобка '{' блока операторов.");
            if (stmts.Count == 0)
                throw CreateError(openBracePos, "Блок '{ ... }' не должен быть пустым — укажите хотя бы один оператор.");
            return stmts.Count == 1 ? stmts[0] : new SequenceStatement(stmts);
        }
        return ParseStatement();
    }

    public Condition ParseFullCondition()
    {
        ValidateBrackets();
        var cond = ParseCondition();
        SkipWhitespace();
        if (_pos < _text.Length)
        {
            throw CreateError(_pos, $"Неожиданный фрагмент '{GetTokenPreview(_pos)}' в логическом условии.");
        }
        return cond;
    }

    public Condition ParseCondition()
    {
        return ParseOr();
    }

    private Condition ParseOr()
    {
        var left = ParseAnd();
        SkipWhitespace();
        while (Match("||") || Match("∨") || MatchWord("or"))
        {
            var right = ParseAnd();
            left = new LogicalCondition(left, LogicalOp.Or, right);
            SkipWhitespace();
        }
        return left;
    }

    private Condition ParseAnd()
    {
        var left = ParseUnaryCond();
        SkipWhitespace();
        while (Match("&&") || Match("∧") || MatchWord("and"))
        {
            var right = ParseUnaryCond();
            left = new LogicalCondition(left, LogicalOp.And, right);
            SkipWhitespace();
        }
        return left;
    }

    private Condition ParseUnaryCond()
    {
        SkipWhitespace();
        if (Match("!") || Match("¬") || MatchWord("not"))
        {
            var inner = ParseUnaryCond();
            return new NotCondition(inner);
        }

        if (Match("("))
        {
            int backtrackPos = _pos;
            int savedLastToken = _lastTokenEndPos;
            try
            {
                var cond = ParseCondition();
                SkipWhitespace();
                if (Match(")"))
                {
                    SkipWhitespace();
                    if (!IsRelationalOpNext())
                    {
                        return cond;
                    }
                }
            }
            catch
            {
                // Откат к попытке распарсить как арифметическое выражение в скобках
            }
            _pos = backtrackPos - 1;
            _lastTokenEndPos = savedLastToken;
        }

        return ParseRelational();
    }

    private bool IsRelationalOpNext()
    {
        return Peek("==") || Peek("!=") || Peek("≠") || Peek(">=") || Peek("≥") ||
               Peek("<=") || Peek("≤") || Peek(">") || Peek("<");
    }

    private Condition ParseRelational()
    {
        var left = ParseExpr();
        int afterLeftPos = _lastTokenEndPos;
        SkipWhitespace();

        RelationalOp op;
        if (Match("==") || Match("=")) op = RelationalOp.Equal;
        else if (Match("!=") || Match("≠")) op = RelationalOp.NotEqual;
        else if (Match(">=") || Match("≥")) op = RelationalOp.GreaterOrEqual;
        else if (Match("<=") || Match("≤")) op = RelationalOp.LessOrEqual;
        else if (Match(">")) op = RelationalOp.Greater;
        else if (Match("<")) op = RelationalOp.Less;
        else
        {
            if (left is VariableExpr v && v.Name.Equals("true", StringComparison.OrdinalIgnoreCase))
                return new BooleanLiteralCondition(true);
            if (left is VariableExpr v2 && v2.Name.Equals("false", StringComparison.OrdinalIgnoreCase))
                return new BooleanLiteralCondition(false);

            throw CreateError(afterLeftPos, $"После выражения '{left}' ожидался оператор сравнения (==, !=, <, >, <=, >=).");
        }

        var right = ParseExpr();
        return new RelationalCondition(left, op, right);
    }

    public Expr ParseExpr()
    {
        return ParseAddSub();
    }

    private Expr ParseAddSub()
    {
        var left = ParseMulDiv();
        SkipWhitespace();
        while (true)
        {
            if (Match("+"))
            {
                var right = ParseMulDiv();
                left = new BinaryExpr(left, BinaryOp.Add, right);
            }
            else if (Match("-"))
            {
                var right = ParseMulDiv();
                left = new BinaryExpr(left, BinaryOp.Sub, right);
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }
        return left;
    }

    private Expr ParseMulDiv()
    {
        var left = ParsePrimary();
        SkipWhitespace();
        while (true)
        {
            if (Match("*"))
            {
                var right = ParsePrimary();
                left = new BinaryExpr(left, BinaryOp.Mul, right);
            }
            else if (Match("/"))
            {
                var right = ParsePrimary();
                left = new BinaryExpr(left, BinaryOp.Div, right);
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }
        return left;
    }

    private Expr ParsePrimary()
    {
        int beforeSkipPos = _lastTokenEndPos;
        SkipWhitespace();

        if (_pos >= _text.Length)
            throw CreateError(beforeSkipPos, "Неожиданный конец строки — ожидалось выражение (число или переменная).");

        // Если после оператора (:=, +, -, *, /) строка оборвалась и на следующей строке идет '}' или ';' или 'else'
        if (_text[_pos] == '}' || _text[_pos] == ';' || PeekWord("else"))
        {
            int errPos = HasNewlineBetween(beforeSkipPos, _pos) ? beforeSkipPos : _pos;
            throw CreateError(errPos, "Пропущено выражение после оператора.");
        }

        if (Match("("))
        {
            var expr = ParseExpr();
            SkipWhitespace();
            if (!Match(")"))
                throw CreateError(_lastTokenEndPos, "Ожидалась закрывающая скобка ')'.");
            return expr;
        }

        if (Peek("abs("))
        {
            Match("abs");
            Match("(");
            var inner = ParseExpr();
            SkipWhitespace();
            if (!Match(")"))
                throw CreateError(_lastTokenEndPos, "Ожидалась закрывающая скобка ')' в вызове abs().");
            return new AbsExpr(inner);
        }

        // Унарный минус (например, -5 или -x)
        if (Match("-"))
        {
            var inner = ParsePrimary();
            return new BinaryExpr(new NumberExpr(0), BinaryOp.Sub, inner);
        }

        // Число
        if (char.IsDigit(_text[_pos]))
        {
            int start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                _pos++;
            _lastTokenEndPos = _pos;
            string numStr = _text.Substring(start, _pos - start);
            if (double.TryParse(numStr, System.Globalization.CultureInfo.InvariantCulture, out double val))
                return new NumberExpr(val);
            throw CreateError(start, $"Некорректная запись числа '{numStr}'.");
        }

        // Идентификатор
        if (char.IsLetter(_text[_pos]) || _text[_pos] == '_')
        {
            string id = ParseIdentifier();
            return new VariableExpr(id);
        }

        throw CreateError(_pos, $"Недопустимый символ '{_text[_pos]}' в выражении.");
    }

    private string ParseIdentifier()
    {
        int beforeSkipPos = _lastTokenEndPos;
        SkipWhitespace();
        int start = _pos;
        while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
            _pos++;

        if (start == _pos)
        {
            int errPos = _pos >= _text.Length ? beforeSkipPos : _pos;
            string found = _pos < _text.Length ? $" (встречено '{_text[_pos]}')" : string.Empty;
            throw CreateError(errPos, $"Ожидалось имя переменной{found}.");
        }

        _lastTokenEndPos = _pos;
        return _text.Substring(start, _pos - start);
    }
}
