using WpEngine.Core.AST;
using WpEngine.Core.Parsing;

namespace WpEngine.Tests;

/// <summary>
/// Тесты парсера: построение AST из потока токенов.
/// </summary>
public class ParserTests
{
    // ─── Вспомогательные методы ──────────────────────────────────────

    private static Expr ParseExpr(string input)
    {
        var tokens = Lexer.Tokenize(input);
        return new Parser(tokens).ParseExpression();
    }

    private static Condition ParseCond(string input)
    {
        var tokens = Lexer.Tokenize(input);
        var parser = new Parser(tokens);
        var cond = parser.ParseCondition();
        parser.ExpectEnd();
        return cond;
    }

    private static Statement ParseProg(string input)
    {
        var tokens = Lexer.Tokenize(input);
        return new Parser(tokens).ParseProgram();
    }

    // ─── ParseExpression: числа и переменные ─────────────────────────

    [Fact]
    public void ParseExpression_NumberLiteral_ReturnsNumberExpr()
    {
        var result = ParseExpr("42");

        var number = Assert.IsType<NumberExpr>(result);
        Assert.Equal(42.0, number.Value);
    }

    [Fact]
    public void ParseExpression_FloatLiteral_ReturnsCorrectValue()
    {
        var result = ParseExpr("3.14");

        var number = Assert.IsType<NumberExpr>(result);
        Assert.Equal(3.14, number.Value, precision: 10);
    }

    [Fact]
    public void ParseExpression_VariableName_ReturnsVariableExpr()
    {
        var result = ParseExpr("x");

        var variable = Assert.IsType<VariableExpr>(result);
        Assert.Equal("x", variable.Name);
    }

    [Fact]
    public void ParseExpression_UnaryMinus_NegatesNumber()
    {
        var result = ParseExpr("-5");

        var number = Assert.IsType<NumberExpr>(result);
        Assert.Equal(-5.0, number.Value);
    }

    [Fact]
    public void ParseExpression_UnaryMinusVariable_ReturnsBinarySubFrom0()
    {
        var result = ParseExpr("-x");

        var binary = Assert.IsType<BinaryExpr>(result);
        Assert.Equal(BinaryOp.Sub, binary.Op);
        Assert.IsType<NumberExpr>(binary.Left);  // 0
        Assert.IsType<VariableExpr>(binary.Right);
    }

    // ─── ParseExpression: арифметика ─────────────────────────────────

    [Fact]
    public void ParseExpression_Addition_ReturnsBinaryAdd()
    {
        var result = ParseExpr("a + b");

        var binary = Assert.IsType<BinaryExpr>(result);
        Assert.Equal(BinaryOp.Add, binary.Op);
    }

    [Fact]
    public void ParseExpression_MultipleAdditions_LeftAssociative()
    {
        // "a + b + c" → (a + b) + c
        var result = ParseExpr("a + b + c");

        var outer = Assert.IsType<BinaryExpr>(result);
        Assert.IsType<BinaryExpr>(outer.Left);
        Assert.IsType<VariableExpr>(outer.Right);
    }

    [Fact]
    public void ParseExpression_MulBeforeAdd_CorrectPrecedence()
    {
        // "a + b * c" → a + (b * c)
        var result = ParseExpr("a + b * c");

        var add = Assert.IsType<BinaryExpr>(result);
        Assert.Equal(BinaryOp.Add, add.Op);
        var mul = Assert.IsType<BinaryExpr>(add.Right);
        Assert.Equal(BinaryOp.Mul, mul.Op);
    }

    [Fact]
    public void ParseExpression_Parentheses_OverridePrecedence()
    {
        // "(a + b) * c" → mul(add(a,b), c)
        var result = ParseExpr("(a + b) * c");

        var mul = Assert.IsType<BinaryExpr>(result);
        Assert.Equal(BinaryOp.Mul, mul.Op);
        var add = Assert.IsType<BinaryExpr>(mul.Left);
        Assert.Equal(BinaryOp.Add, add.Op);
    }

    [Fact]
    public void ParseExpression_AbsFunction_ReturnsAbsExpr()
    {
        var result = ParseExpr("abs(x)");

        var abs = Assert.IsType<AbsExpr>(result);
        var inner = Assert.IsType<VariableExpr>(abs.Inner);
        Assert.Equal("x", inner.Name);
    }

    [Fact]
    public void ParseExpression_AbsWithExpression_ParsesInner()
    {
        var result = ParseExpr("abs(x - 5)");

        var abs = Assert.IsType<AbsExpr>(result);
        Assert.IsType<BinaryExpr>(abs.Inner);
    }

    [Fact]
    public void ParseExpression_UnknownFunction_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => ParseExpr("sqrt(x)"));
    }

    // ─── ParseCondition: сравнения ────────────────────────────────────

    [Theory]
    [InlineData("x == 5", RelationalOp.Equal)]
    [InlineData("x != 5", RelationalOp.NotEqual)]
    [InlineData("x < 5", RelationalOp.Less)]
    [InlineData("x <= 5", RelationalOp.LessOrEqual)]
    [InlineData("x > 5", RelationalOp.Greater)]
    [InlineData("x >= 5", RelationalOp.GreaterOrEqual)]
    public void ParseCondition_RelationalOps_ReturnsCorrectOp(string input, RelationalOp expected)
    {
        var cond = ParseCond(input);

        var rel = Assert.IsType<RelationalCondition>(cond);
        Assert.Equal(expected, rel.Op);
    }

    [Fact]
    public void ParseCondition_AndCondition_ReturnsLogicalAnd()
    {
        var cond = ParseCond("x > 0 && y < 10");

        var logical = Assert.IsType<LogicalCondition>(cond);
        Assert.Equal(LogicalOp.And, logical.Op);
    }

    [Fact]
    public void ParseCondition_OrCondition_ReturnsLogicalOr()
    {
        var cond = ParseCond("x > 0 || y < 10");

        var logical = Assert.IsType<LogicalCondition>(cond);
        Assert.Equal(LogicalOp.Or, logical.Op);
    }

    [Fact]
    public void ParseCondition_AndBeforeOr_CorrectPrecedence()
    {
        // "a || b && c" → a || (b && c)
        var cond = ParseCond("x > 0 || y > 0 && z > 0");

        var or = Assert.IsType<LogicalCondition>(cond);
        Assert.Equal(LogicalOp.Or, or.Op);
        var and = Assert.IsType<LogicalCondition>(or.Right);
        Assert.Equal(LogicalOp.And, and.Op);
    }

    [Fact]
    public void ParseCondition_NotOperator_ReturnsNotCondition()
    {
        var cond = ParseCond("!(x > 0)");

        Assert.IsType<NotCondition>(cond);
    }

    [Fact]
    public void ParseCondition_DoubleNot_WrapsInTwoNot()
    {
        var cond = ParseCond("!!(x > 0)");

        var outer = Assert.IsType<NotCondition>(cond);
        Assert.IsType<NotCondition>(outer.Inner);
    }

    [Fact]
    public void ParseCondition_TrueLiteral_ReturnsBooleanTrue()
    {
        var cond = ParseCond("true");

        var lit = Assert.IsType<BooleanLiteralCondition>(cond);
        Assert.True(lit.Value);
    }

    [Fact]
    public void ParseCondition_FalseLiteral_ReturnsBooleanFalse()
    {
        var cond = ParseCond("false");

        var lit = Assert.IsType<BooleanLiteralCondition>(cond);
        Assert.False(lit.Value);
    }

    // ─── ParseProgram: операторы ──────────────────────────────────────

    [Fact]
    public void ParseProgram_SimpleAssignment_ReturnsAssignmentStatement()
    {
        var stmt = ParseProg("x := 5");

        var assign = Assert.IsType<AssignmentStatement>(stmt);
        Assert.Equal("x", assign.Variable);
        var val = Assert.IsType<NumberExpr>(assign.Value);
        Assert.Equal(5.0, val.Value);
    }

    [Fact]
    public void ParseProgram_AssignmentWithExpr_ParsesRhs()
    {
        var stmt = ParseProg("x := a + 1");

        var assign = Assert.IsType<AssignmentStatement>(stmt);
        Assert.IsType<BinaryExpr>(assign.Value);
    }

    [Fact]
    public void ParseProgram_TwoStatements_ReturnsSequence()
    {
        var stmt = ParseProg("x := 1; y := 2");

        var seq = Assert.IsType<SequenceStatement>(stmt);
        Assert.Equal(2, seq.Statements.Count);
    }

    [Fact]
    public void ParseProgram_ThreeStatements_SequenceHasThree()
    {
        var stmt = ParseProg("x := 1; y := 2; z := 3");

        var seq = Assert.IsType<SequenceStatement>(stmt);
        Assert.Equal(3, seq.Statements.Count);
    }

    [Fact]
    public void ParseProgram_IfElse_ReturnsIfStatement()
    {
        var stmt = ParseProg("if (x > 0) { y := 1 } else { y := 0 }");

        var ifStmt = Assert.IsType<IfStatement>(stmt);
        Assert.IsType<RelationalCondition>(ifStmt.Condition);
        Assert.IsType<AssignmentStatement>(ifStmt.ThenBranch);
        Assert.IsType<AssignmentStatement>(ifStmt.ElseBranch);
    }

    [Fact]
    public void ParseProgram_NestedIfElse_ParsesCorrectly()
    {
        var code = """
            if (x > 0) {
                if (y > 0) { z := 1 } else { z := 2 }
            } else {
                z := 0
            }
            """;
        var stmt = ParseProg(code);

        var outer = Assert.IsType<IfStatement>(stmt);
        Assert.IsType<IfStatement>(outer.ThenBranch);
    }

    [Fact]
    public void ParseProgram_EmptyInput_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => ParseProg(""));
    }

    [Fact]
    public void ParseProgram_ElseWithoutIf_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => ParseProg("else { x := 1 }"));
    }

    [Fact]
    public void ParseProgram_MissingSemicolon_ThrowsParseException()
    {
        // Два присваивания без ";" должны вызвать ошибку
        Assert.Throws<ParseException>(() => ParseProg("x := 1 y := 2"));
    }

    [Fact]
    public void ParseProgram_IfWithoutElse_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => ParseProg("if (x > 0) { y := 1 }"));
    }
}
