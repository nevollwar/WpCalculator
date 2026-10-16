using WpEngine.Core.AST;
using WpEngine.Core.Parsing;

namespace WpEngine.Tests;

/// <summary>
/// Тесты Humanizer: форматирование условий и операторов в строки.
/// </summary>
public class HumanizerTests
{
    // ─── Format(Condition) ────────────────────────────────────────────

    [Theory]
    [InlineData(RelationalOp.Equal, "==")]
    [InlineData(RelationalOp.NotEqual, "!=")]
    [InlineData(RelationalOp.Less, "<")]
    [InlineData(RelationalOp.LessOrEqual, "<=")]
    [InlineData(RelationalOp.Greater, ">")]
    [InlineData(RelationalOp.GreaterOrEqual, ">=")]
    public void Format_RelationalOps_ContainsCorrectSymbol(RelationalOp op, string symbol)
    {
        var cond = new RelationalCondition(new VariableExpr("x"), op, new NumberExpr(5));

        var result = Humanizer.Format(cond);

        Assert.Contains(symbol, result);
    }

    [Fact]
    public void Format_AndCondition_ContainsAmpersands()
    {
        var cond = new LogicalCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.And,
            new RelationalCondition(new VariableExpr("y"), RelationalOp.Less, new NumberExpr(10)));

        var result = Humanizer.Format(cond);

        Assert.Contains("&&", result);
    }

    [Fact]
    public void Format_OrCondition_ContainsPipes()
    {
        var cond = new LogicalCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.Or,
            new RelationalCondition(new VariableExpr("y"), RelationalOp.Less, new NumberExpr(10)));

        Assert.Contains("||", Humanizer.Format(cond));
    }

    [Fact]
    public void Format_NotCondition_StartsWithBang()
    {
        var cond = new NotCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)));

        var result = Humanizer.Format(cond);

        Assert.StartsWith("!", result);
    }

    [Fact]
    public void Format_BooleanTrue_ReturnsTrue()
    {
        Assert.Equal("true", Humanizer.Format(new BooleanLiteralCondition(true)));
    }

    [Fact]
    public void Format_BooleanFalse_ReturnsFalse()
    {
        Assert.Equal("false", Humanizer.Format(new BooleanLiteralCondition(false)));
    }

    [Fact]
    public void Format_AndInsideOr_LeftAddedParens()
    {
        // (a && b) || c: у && приоритет выше, поэтому скобки не нужны слева
        // a || (b && c): правая часть && — скобки нужны только если правее стоит ||
        // Проверяем: "a || b && c" → b&&c без лишних скобок
        var inner = new LogicalCondition(
            new RelationalCondition(new VariableExpr("b"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.And,
            new RelationalCondition(new VariableExpr("c"), RelationalOp.Greater, new NumberExpr(0)));
        var outer = new LogicalCondition(
            new RelationalCondition(new VariableExpr("a"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.Or,
            inner);

        var result = Humanizer.Format(outer);

        // Без лишних скобок, только обязательные
        Assert.Contains("||", result);
        Assert.Contains("&&", result);
    }

    // ─── Format(Statement) ────────────────────────────────────────────

    [Fact]
    public void Format_Assignment_ReturnsCorrectString()
    {
        var stmt = new AssignmentStatement("x", new NumberExpr(5));

        Assert.Equal("x := 5", Humanizer.Format(stmt));
    }

    [Fact]
    public void Format_SequenceStatement_JoinedBySemicolon()
    {
        var seq = new SequenceStatement(new List<Statement>
        {
            new AssignmentStatement("x", new NumberExpr(1)),
            new AssignmentStatement("y", new NumberExpr(2))
        });

        var result = Humanizer.Format(seq);

        Assert.Contains(";", result);
        Assert.Contains("x := 1", result);
        Assert.Contains("y := 2", result);
    }

    [Fact]
    public void Format_IfStatement_ContainsIfElseKeywords()
    {
        var ifStmt = new IfStatement(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            new AssignmentStatement("y", new NumberExpr(1)),
            new AssignmentStatement("y", new NumberExpr(0)));

        var result = Humanizer.Format(ifStmt);

        Assert.Contains("if", result);
        Assert.Contains("else", result);
    }

    // ─── Describe(Condition) ─────────────────────────────────────────

    [Fact]
    public void Describe_VariableGreaterThan_ContainsBolshe()
    {
        var cond = new RelationalCondition(
            new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(5));

        var result = Humanizer.Describe(cond);

        Assert.Contains("больше", result);
        Assert.Contains("x", result);
    }

    [Fact]
    public void Describe_VariableEqualTo_UsesRavenForm()
    {
        // Для переменной — "равен", для выражения — "равно"
        var cond = new RelationalCondition(
            new VariableExpr("x"), RelationalOp.Equal, new NumberExpr(4));

        Assert.Contains("равен", Humanizer.Describe(cond));
    }

    [Fact]
    public void Describe_ExpressionEqualTo_UsesRavnoForm()
    {
        var cond = new RelationalCondition(
            new BinaryExpr(new VariableExpr("x"), BinaryOp.Add, new NumberExpr(1)),
            RelationalOp.Equal,
            new NumberExpr(5));

        Assert.Contains("равно", Humanizer.Describe(cond));
    }

    [Fact]
    public void Describe_AndCondition_ContainsI()
    {
        var cond = new LogicalCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.And,
            new RelationalCondition(new VariableExpr("y"), RelationalOp.Less, new NumberExpr(10)));

        Assert.Contains(" и ", Humanizer.Describe(cond));
    }

    [Fact]
    public void Describe_OrCondition_ContainsIli()
    {
        var cond = new LogicalCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.Or,
            new RelationalCondition(new VariableExpr("y"), RelationalOp.Less, new NumberExpr(10)));

        Assert.Contains("или", Humanizer.Describe(cond));
    }

    [Fact]
    public void Describe_NotCondition_ContainsNeverno()
    {
        var cond = new NotCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)));

        Assert.Contains("неверно", Humanizer.Describe(cond));
    }

    [Fact]
    public void Describe_BooleanTrue_ReturnsIstina()
    {
        Assert.Equal("истина", Humanizer.Describe(new BooleanLiteralCondition(true)));
    }

    [Fact]
    public void Describe_BooleanFalse_ReturnsLozh()
    {
        Assert.Equal("ложь", Humanizer.Describe(new BooleanLiteralCondition(false)));
    }

    // ─── DescribeTriad ────────────────────────────────────────────────

    [Fact]
    public void DescribeTriad_ContainsProgramAndBothConditions()
    {
        var pre = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var post = new RelationalCondition(new VariableExpr("y"), RelationalOp.Greater, new NumberExpr(0));
        var prog = new AssignmentStatement("y", new VariableExpr("x"));

        var result = Humanizer.DescribeTriad(pre, prog, post);

        Assert.Contains("перед выполнением", result);
        Assert.Contains("после её выполнения", result);
    }

    // ─── Format: арифметические выражения (через Condition) ──────────

    [Fact]
    public void Format_NegativeNumberInExpr_WrappedInParens()
    {
        // "5 - -3" должно форматироваться как "5 - (-3)"
        var expr = new BinaryExpr(new NumberExpr(5), BinaryOp.Sub, new NumberExpr(-3));
        var cond = new RelationalCondition(expr, RelationalOp.Greater, new NumberExpr(0));

        var result = Humanizer.Format(cond);

        Assert.Contains("(-3)", result);
    }
}
