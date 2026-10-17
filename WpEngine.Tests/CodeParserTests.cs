using WpEngine.Core.AST;
using WpEngine.Core.Parsing;

namespace WpEngine.Tests;

/// <summary>
/// Тесты CodeParser: публичный фасад над Lexer + Parser.
/// </summary>
public class CodeParserTests
{
    private readonly CodeParser _parser = new();

    // ─── ParseProgram ─────────────────────────────────────────────────

    [Fact]
    public void ParseProgram_SingleAssignment_ReturnsParsedStatement()
    {
        var result = _parser.ParseProgram("x := 42");

        var assign = Assert.IsType<AssignmentStatement>(result);
        Assert.Equal("x", assign.Variable);
    }

    [Fact]
    public void ParseProgram_Sequence_ReturnsAllStatements()
    {
        var result = _parser.ParseProgram("a := 1; b := 2; c := 3");

        var seq = Assert.IsType<SequenceStatement>(result);
        Assert.Equal(3, seq.Statements.Count);
    }

    [Fact]
    public void ParseProgram_IfElseBlock_ParsesConditionAndBranches()
    {
        var result = _parser.ParseProgram("if (d >= 0) { x := 1 } else { x := 0 }");

        var ifStmt = Assert.IsType<IfStatement>(result);
        var cond = Assert.IsType<RelationalCondition>(ifStmt.Condition);
        Assert.Equal(RelationalOp.GreaterOrEqual, cond.Op);
    }

    [Fact]
    public void ParseProgram_EmptyString_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => _parser.ParseProgram(""));
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void ParseProgram_OnlyWhitespace_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => _parser.ParseProgram("   "));
    }

    [Fact]
    public void ParseProgram_TrailingGarbage_ThrowsParseException()
    {
        // После корректной программы стоит лишний токен
        Assert.Throws<ParseException>(() => _parser.ParseProgram("x := 1 }"));
    }

    // ─── ParseCondition ───────────────────────────────────────────────

    [Fact]
    public void ParseCondition_SimpleRelational_ReturnsParsedCondition()
    {
        var result = _parser.ParseCondition("x > 100");

        var rel = Assert.IsType<RelationalCondition>(result);
        Assert.Equal(RelationalOp.Greater, rel.Op);
    }

    [Fact]
    public void ParseCondition_CompoundAndOr_ParsedCorrectly()
    {
        var result = _parser.ParseCondition("x > 0 && y < 10");

        var logical = Assert.IsType<LogicalCondition>(result);
        Assert.Equal(LogicalOp.And, logical.Op);
    }

    [Fact]
    public void ParseCondition_TrueLiteral_ReturnsBoolTrue()
    {
        var result = _parser.ParseCondition("true");

        var lit = Assert.IsType<BooleanLiteralCondition>(result);
        Assert.True(lit.Value);
    }

    [Fact]
    public void ParseCondition_EmptyString_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => _parser.ParseCondition(""));
    }

    [Fact]
    public void ParseCondition_TrailingGarbage_ThrowsParseException()
    {
        // "x > 5 )" — после условия остался лишний ")"
        Assert.Throws<ParseException>(() => _parser.ParseCondition("x > 5 )"));
    }

    [Fact]
    public void ParseCondition_ComplexNested_ParsesWithoutException()
    {
        // Проверяем, что сложное вложенное условие парсится без падений
        var result = _parser.ParseCondition("(x > 0 && y > 0) || z == 1");
        Assert.NotNull(result);
    }
}
