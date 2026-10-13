namespace WpEngine.Core.Parsing;

/// <summary>
/// Типы токенов, которые выдаёт лексер.
/// </summary>
public enum TokenType
{
    Number,         // 42, 3.14
    Identifier,     // x, y, z. Так же if и else

    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /

    Assign,         // :=
    Equal,          // ==
    NotEqual,       // !=
    Less,           // <
    LessOrEqual,    // <=
    Greater,        // >
    GreaterOrEqual, // >=

    And,            // &&
    Or,             // ||
    Not,            // !

    LeftParen,      // (
    RightParen,     // )
    LeftBrace,      // {
    RightBrace,     // }
    Semicolon,      // ;

    End             // конец входной строки (EOF)
}

/// <summary>
/// Токен: минимальная значимая единица входного текста.
/// </summary>
/// <param name="Type">Тип токена.</param>
/// <param name="Text">Текст токена в том виде, как он был введён (например, "x" или ":=").</param>
/// <param name="Position">Индекс первого символа токена во входной строке, с нуля. Нужен для сообщений об ошибках.</param>
public record Token(TokenType Type, string Text, int Position);
