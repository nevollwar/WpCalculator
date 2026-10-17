using WpEngine.Core.Parsing;

namespace WpEngine.Tests;

/// <summary>
/// Тесты лексера: разбивка входного текста на токены.
/// </summary>
public class LexerTests
{
    //Tokenize: числа

    [Fact]
    public void Tokenize_Integer_ReturnsNumberToken()
    {
        var tokens = Lexer.Tokenize("42");

        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal("42", tokens[0].Text);
        Assert.Equal(TokenType.End, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_FloatWithDot_ReturnsNumberToken()
    {
        var tokens = Lexer.Tokenize("3.14");

        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal("3.14", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_NegativeNumberAsExpression_ReturnsSeparateMinusAndNumber()
    {
        // Лексер не знает о знаке числа — это задача парсера
        var tokens = Lexer.Tokenize("-5");

        Assert.Equal(TokenType.Minus, tokens[0].Type);
        Assert.Equal(TokenType.Number, tokens[1].Type);
        Assert.Equal("5", tokens[1].Text);
    }

    [Fact]
    public void Tokenize_NumberFollowedByDotWithoutDigit_ThrowsParseException()
    {
        // "5." — целое "5" читается нормально, затем точка без цифры после неё
        // не является дробным разделителем и вызывает ParseException
        Assert.Throws<ParseException>(() => Lexer.Tokenize("5."));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("1.5")]
    [InlineData("999")]
    public void Tokenize_VariousNumbers_AllParsedAsNumber(string input)
    {
        var tokens = Lexer.Tokenize(input);
        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal(input, tokens[0].Text);
    }

    //Tokenize: идентификаторы

    [Fact]
    public void Tokenize_SimpleName_ReturnsIdentifierToken()
    {
        var tokens = Lexer.Tokenize("x");

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("x", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_MultiCharName_ReturnsFullIdentifier()
    {
        var tokens = Lexer.Tokenize("myVar");

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("myVar", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_NameWithUnderscore_ReturnsIdentifier()
    {
        var tokens = Lexer.Tokenize("_x1");

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("_x1", tokens[0].Text);
    }

    [Theory]
    [InlineData("if")]
    [InlineData("else")]
    [InlineData("true")]
    [InlineData("false")]
    public void Tokenize_Keywords_ReturnedAsIdentifiers(string keyword)
    {
        // Ключевые слова определяет парсер, лексер отдаёт их как обычные имена
        var tokens = Lexer.Tokenize(keyword);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(keyword, tokens[0].Text);
    }

    //Tokenize: операторы однознаковые

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Star)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("<", TokenType.Less)]
    [InlineData(">", TokenType.Greater)]
    [InlineData("!", TokenType.Not)]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    [InlineData("{", TokenType.LeftBrace)]
    [InlineData("}", TokenType.RightBrace)]
    [InlineData(";", TokenType.Semicolon)]
    public void Tokenize_SingleCharSymbols_CorrectTypes(string input, TokenType expected)
    {
        var tokens = Lexer.Tokenize(input);
        Assert.Equal(expected, tokens[0].Type);
    }

    //Tokenize: операторы двухзнаковые

    [Theory]
    [InlineData(":=", TokenType.Assign)]
    [InlineData("==", TokenType.Equal)]
    [InlineData("!=", TokenType.NotEqual)]
    [InlineData("<=", TokenType.LessOrEqual)]
    [InlineData(">=", TokenType.GreaterOrEqual)]
    [InlineData("&&", TokenType.And)]
    [InlineData("||", TokenType.Or)]
    public void Tokenize_TwoCharSymbols_ParsedAsOneToken(string input, TokenType expected)
    {
        var tokens = Lexer.Tokenize(input);

        Assert.Single(tokens, t => t.Type == expected);
        Assert.Equal(2, tokens.Count); // знак + End
    }

    [Fact]
    public void Tokenize_LessOrEqual_NotSplitIntoTwoTokens()
    {
        // Убеждаемся, что "<=" — один токен, а не "<" + "="
        var tokens = Lexer.Tokenize("<=");

        Assert.Equal(TokenType.LessOrEqual, tokens[0].Type);
        Assert.Equal(2, tokens.Count); // LessOrEqual + End
    }

    //Tokenize: пробелы и конец

    [Fact]
    public void Tokenize_WhitespaceOnly_ReturnsOnlyEndToken()
    {
        var tokens = Lexer.Tokenize("   \t\n");

        Assert.Single(tokens);
        Assert.Equal(TokenType.End, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsOnlyEndToken()
    {
        var tokens = Lexer.Tokenize("");

        Assert.Single(tokens);
        Assert.Equal(TokenType.End, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_Expression_LastTokenIsEnd()
    {
        var tokens = Lexer.Tokenize("x + 1");

        Assert.Equal(TokenType.End, tokens[^1].Type);
    }

    [Fact]
    public void Tokenize_TokenPositions_AreCorrect()
    {
        // "x + 1" → x@0, +@2, 1@4
        var tokens = Lexer.Tokenize("x + 1");

        Assert.Equal(0, tokens[0].Position); // x
        Assert.Equal(2, tokens[1].Position); // +
        Assert.Equal(4, tokens[2].Position); // 1
    }

    //Tokenize: ошибки

    [Fact]
    public void Tokenize_SingleEquals_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x = 5"));
        Assert.Contains("==", ex.Message);
    }

    [Fact]
    public void Tokenize_SingleAmpersand_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x & y"));
        Assert.Contains("&&", ex.Message);
    }

    [Fact]
    public void Tokenize_SinglePipe_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x | y"));
        Assert.Contains("||", ex.Message);
    }

    [Fact]
    public void Tokenize_SingleColon_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x : 5"));
        Assert.Contains(":=", ex.Message);
    }

    [Fact]
    public void Tokenize_UnknownSymbol_ThrowsParseException()
    {
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x @ y"));
        Assert.Contains("@", ex.Message);
    }

    [Fact]
    public void Tokenize_ParseException_PositionIsCorrect()
    {
        // "x = 5" → '=' стоит на позиции 2
        var ex = Assert.Throws<ParseException>(() => Lexer.Tokenize("x = 5"));
        Assert.Equal(2, ex.Position);
    }
}
