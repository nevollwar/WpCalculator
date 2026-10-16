using WpEngine.Core.AST;
using WpEngine.Core.Services;

namespace WpEngine.Tests;

/// <summary>
/// Тесты SubstitutionEngine: подстановка выражений и упрощение предикатов.
/// </summary>
public class SubstitutionEngineTests
{
    private readonly SubstitutionEngine _engine = new();

    // ─── SubstituteExpr ───────────────────────────────────────────────

    [Fact]
    public void SubstituteExpr_MatchingVariable_ReplacedByExpression()
    {
        // x[x -> 5] = 5
        var expr = new VariableExpr("x");
        var replacement = new NumberExpr(5);

        var result = _engine.SubstituteExpr(expr, "x", replacement);

        var number = Assert.IsType<NumberExpr>(result);
        Assert.Equal(5.0, number.Value);
    }

    [Fact]
    public void SubstituteExpr_NonMatchingVariable_Unchanged()
    {
        // y[x -> 5] = y
        var expr = new VariableExpr("y");
        var replacement = new NumberExpr(5);

        var result = _engine.SubstituteExpr(expr, "x", replacement);

        var variable = Assert.IsType<VariableExpr>(result);
        Assert.Equal("y", variable.Name);
    }

    [Fact]
    public void SubstituteExpr_Number_Unchanged()
    {
        // 42[x -> 5] = 42
        var expr = new NumberExpr(42);

        var result = _engine.SubstituteExpr(expr, "x", new NumberExpr(5));

        var number = Assert.IsType<NumberExpr>(result);
        Assert.Equal(42.0, number.Value);
    }

    [Fact]
    public void SubstituteExpr_BinaryExprBothSides_BothReplaced()
    {
        // (x + x)[x -> 3] = (3 + 3)
        var expr = new BinaryExpr(new VariableExpr("x"), BinaryOp.Add, new VariableExpr("x"));
        var replacement = new NumberExpr(3);

        var result = _engine.SubstituteExpr(expr, "x", replacement);

        var binary = Assert.IsType<BinaryExpr>(result);
        var left = Assert.IsType<NumberExpr>(binary.Left);
        var right = Assert.IsType<NumberExpr>(binary.Right);
        Assert.Equal(3.0, left.Value);
        Assert.Equal(3.0, right.Value);
    }

    [Fact]
    public void SubstituteExpr_AbsExpr_SubstitutesInsideAbs()
    {
        // abs(x)[x -> 7] = abs(7)
        var expr = new AbsExpr(new VariableExpr("x"));

        var result = _engine.SubstituteExpr(expr, "x", new NumberExpr(7));

        var abs = Assert.IsType<AbsExpr>(result);
        var inner = Assert.IsType<NumberExpr>(abs.Inner);
        Assert.Equal(7.0, inner.Value);
    }

    [Fact]
    public void SubstituteExpr_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _engine.SubstituteExpr(null!, "x", new NumberExpr(1)));
    }

    // ─── Substitute(Condition) ────────────────────────────────────────

    [Fact]
    public void Substitute_RelationalCondition_BothSidesSubstituted()
    {
        // (x > x)[x -> 5] → (5 > 5)
        var cond = new RelationalCondition(
            new VariableExpr("x"), RelationalOp.Greater, new VariableExpr("x"));

        var result = _engine.Substitute(cond, "x", new NumberExpr(5));

        var rel = Assert.IsType<RelationalCondition>(result);
        Assert.IsType<NumberExpr>(rel.Left);
        Assert.IsType<NumberExpr>(rel.Right);
    }

    [Fact]
    public void Substitute_LogicalAndCondition_BothBranchesSubstituted()
    {
        // (x > 0 && x < 10)[x -> 5] → (5 > 0 && 5 < 10)
        var cond = new LogicalCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)),
            LogicalOp.And,
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Less, new NumberExpr(10)));

        var result = _engine.Substitute(cond, "x", new NumberExpr(5));

        var logical = Assert.IsType<LogicalCondition>(result);
        var left = Assert.IsType<RelationalCondition>(logical.Left);
        var right = Assert.IsType<RelationalCondition>(logical.Right);
        Assert.IsType<NumberExpr>(left.Left);
        Assert.IsType<NumberExpr>(right.Left);
    }

    [Fact]
    public void Substitute_NotCondition_InnerSubstituted()
    {
        // !(x > 0)[x -> 5] → !(5 > 0)
        var cond = new NotCondition(
            new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0)));

        var result = _engine.Substitute(cond, "x", new NumberExpr(5));

        var not = Assert.IsType<NotCondition>(result);
        var inner = Assert.IsType<RelationalCondition>(not.Inner);
        Assert.IsType<NumberExpr>(inner.Left);
    }

    [Fact]
    public void Substitute_BooleanLiteral_Unchanged()
    {
        // true[x -> 5] = true
        var cond = new BooleanLiteralCondition(true);

        var result = _engine.Substitute(cond, "x", new NumberExpr(5));

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.True(lit.Value);
    }

    [Fact]
    public void Substitute_OtherVariable_NotAffected()
    {
        // (y > 0)[x -> 5] = (y > 0)
        var cond = new RelationalCondition(
            new VariableExpr("y"), RelationalOp.Greater, new NumberExpr(0));

        var result = _engine.Substitute(cond, "x", new NumberExpr(5));

        var rel = Assert.IsType<RelationalCondition>(result);
        var left = Assert.IsType<VariableExpr>(rel.Left);
        Assert.Equal("y", left.Name);
    }

    [Fact]
    public void Substitute_NullCondition_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _engine.Substitute(null!, "x", new NumberExpr(1)));
    }

    [Fact]
    public void Substitute_NullVariable_ThrowsArgumentException()
    {
        // ThrowIfNullOrWhiteSpace на null бросает ArgumentNullException (подвид ArgumentException)
        var cond = new BooleanLiteralCondition(true);
        Assert.ThrowsAny<ArgumentException>(() =>
            _engine.Substitute(cond, null!, new NumberExpr(1)));
    }

    [Fact]
    public void Substitute_WhitespaceVariable_ThrowsArgumentException()
    {
        var cond = new BooleanLiteralCondition(true);
        Assert.Throws<ArgumentException>(() =>
            _engine.Substitute(cond, "   ", new NumberExpr(1)));
    }

    // ─── Simplify ─────────────────────────────────────────────────────

    [Fact]
    public void Simplify_DoubleNot_RemovesBothNots()
    {
        // !(!A) → A
        var inner = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new NotCondition(new NotCondition(inner));

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }

    [Fact]
    public void Simplify_AndWithTrueOnRight_RemovesTrue()
    {
        // A && true → A
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(a, LogicalOp.And, new BooleanLiteralCondition(true));

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }

    [Fact]
    public void Simplify_AndWithTrueOnLeft_RemovesTrue()
    {
        // true && A → A
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(new BooleanLiteralCondition(true), LogicalOp.And, a);

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }

    [Fact]
    public void Simplify_AndWithFalseOnRight_ReturnsFalse()
    {
        // A && false → false
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(a, LogicalOp.And, new BooleanLiteralCondition(false));

        var result = _engine.Simplify(cond);

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.False(lit.Value);
    }

    [Fact]
    public void Simplify_AndWithFalseOnLeft_ReturnsFalse()
    {
        // false && A → false
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(new BooleanLiteralCondition(false), LogicalOp.And, a);

        var result = _engine.Simplify(cond);

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.False(lit.Value);
    }

    [Fact]
    public void Simplify_OrWithFalseOnRight_RemovesFalse()
    {
        // A || false → A
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(a, LogicalOp.Or, new BooleanLiteralCondition(false));

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }

    [Fact]
    public void Simplify_OrWithFalseOnLeft_RemovesFalse()
    {
        // false || A → A
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(new BooleanLiteralCondition(false), LogicalOp.Or, a);

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }

    [Fact]
    public void Simplify_OrWithTrueOnRight_ReturnsTrue()
    {
        // A || true → true
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(a, LogicalOp.Or, new BooleanLiteralCondition(true));

        var result = _engine.Simplify(cond);

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.True(lit.Value);
    }

    [Fact]
    public void Simplify_OrWithTrueOnLeft_ReturnsTrue()
    {
        // true || A → true
        var a = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new LogicalCondition(new BooleanLiteralCondition(true), LogicalOp.Or, a);

        var result = _engine.Simplify(cond);

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.True(lit.Value);
    }

    [Fact]
    public void Simplify_PlainRelational_Unchanged()
    {
        // Обычное сравнение не должно меняться
        var cond = new RelationalCondition(
            new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));

        var result = _engine.Simplify(cond);

        Assert.Equal(cond, result);
    }

    [Fact]
    public void Simplify_NestedDoubleNot_FullyUnwrapped()
    {
        // !(!(!(!A))) → A
        var inner = new RelationalCondition(new VariableExpr("x"), RelationalOp.Greater, new NumberExpr(0));
        var cond = new NotCondition(new NotCondition(new NotCondition(new NotCondition(inner))));

        var result = _engine.Simplify(cond);

        Assert.IsType<RelationalCondition>(result);
    }
}
