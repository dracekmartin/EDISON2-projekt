using System.Globalization;

namespace TemplateDesigner.Models.Expressions;

// ── Tokeny ────────────────────────────────────────────────────────────────────

internal enum TK
{
    End,
    Variable, StringLit, NumberLit, BoolLit,
    And, Or, Not,
    Eq, Neq, Gt, Lt, Gte, Lte,
    Plus, Minus, Star, Slash,
    LParen, RParen
}

internal readonly struct Token(TK kind, object? value = null)
{
    public TK      Kind  { get; } = kind;
    public object? Value { get; } = value;
}

// ── Tokenizer ────────────────────────────────────────────────────────────────

internal static class Tokenizer
{
    public static List<Token> Tokenize(string expr)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // {FIELD}
            if (c == '{')
            {
                int j = expr.IndexOf('}', i + 1);
                if (j < 0) throw new FormatException($"Neuzavřená závorka '{{' na pozici {i}.");
                tokens.Add(new Token(TK.Variable, expr[(i + 1)..j].Trim()));
                i = j + 1;
                continue;
            }

            // String literal "..." nebo '...'
            if (c == '"' || c == '\'')
            {
                char q = c; i++;
                int start = i;
                while (i < expr.Length && expr[i] != q) i++;
                tokens.Add(new Token(TK.StringLit, expr[start..i]));
                i++; // přeskočit uzavírací uvozovku
                continue;
            }

            // Číslo
            if (char.IsDigit(c))
            {
                int start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                tokens.Add(new Token(TK.NumberLit, double.Parse(expr[start..i], CultureInfo.InvariantCulture)));
                continue;
            }

            // Dvouznaké operátory
            if (i + 1 < expr.Length)
            {
                string two = expr[i..(i + 2)];
                switch (two)
                {
                    case "==": tokens.Add(new Token(TK.Eq));  i += 2; continue;
                    case "!=": tokens.Add(new Token(TK.Neq)); i += 2; continue;
                    case ">=": tokens.Add(new Token(TK.Gte)); i += 2; continue;
                    case "<=": tokens.Add(new Token(TK.Lte)); i += 2; continue;
                    case "&&": tokens.Add(new Token(TK.And)); i += 2; continue;
                    case "||": tokens.Add(new Token(TK.Or));  i += 2; continue;
                }
            }

            // Jednoznakové operátory
            switch (c)
            {
                // Jedno „=" je rovnost jako v Excelu - uživatel ho tak napíše. Jinak by se výraz
                // nepřečetl a podmínka tiše platila vždy (nález Jan 24. 9. u `{STRANA} = 1`).
                case '=': tokens.Add(new Token(TK.Eq));     i++; continue;
                case '>': tokens.Add(new Token(TK.Gt));     i++; continue;
                case '<': tokens.Add(new Token(TK.Lt));     i++; continue;
                case '!': tokens.Add(new Token(TK.Not));    i++; continue;
                case '+': tokens.Add(new Token(TK.Plus));   i++; continue;
                case '-': tokens.Add(new Token(TK.Minus));  i++; continue;
                case '*': tokens.Add(new Token(TK.Star));   i++; continue;
                case '/': tokens.Add(new Token(TK.Slash));  i++; continue;
                case '(': tokens.Add(new Token(TK.LParen)); i++; continue;
                case ')': tokens.Add(new Token(TK.RParen)); i++; continue;
            }

            // Klíčová slova: true / false
            if (char.IsLetter(c))
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                string word = expr[start..i];
                tokens.Add(word switch
                {
                    "true"  => new Token(TK.BoolLit, true),
                    "false" => new Token(TK.BoolLit, false),
                    _       => throw new FormatException($"Neznámý identifikátor '{word}'. Pro proměnné použijte {{{word}}}.")
                });
                continue;
            }

            throw new FormatException($"Neočekávaný znak '{c}' na pozici {i}.");
        }

        tokens.Add(new Token(TK.End));
        return tokens;
    }
}

// ── Recursive-descent parser ─────────────────────────────────────────────────

/// <summary>
/// Parsuje výrazy VisibilityExpression i počítaná pole (aritmetika) do AST.
/// Gramatika (sestupně dle priority):
///   or_expr      = and_expr ('||' and_expr)*
///   and_expr     = not_expr ('&&' not_expr)*
///   not_expr     = '!' not_expr | compare_expr
///   compare_expr = additive (cmp_op additive)?
///   cmp_op       = '==' | '!=' | '>' | '&lt;' | '>=' | '&lt;='
///   additive     = mult (('+' | '-') mult)*
///   mult         = unary (('*' | '/') unary)*
///   unary        = '-' unary | primary
///   primary      = '{IDENT}' | STRING | NUMBER | 'true' | 'false' | '(' expr ')'
/// </summary>
internal sealed class ExpressionParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    private ExpressionParser(List<Token> tokens) => _tokens = tokens;

    public static ExpressionNode Parse(string expr)
    {
        var tokens = Tokenizer.Tokenize(expr.Trim());
        var parser = new ExpressionParser(tokens);
        var node   = parser.ParseOr();
        if (parser.Current.Kind != TK.End)
            throw new FormatException($"Neočekávaný token za výrazem (pozice {parser._pos}).");
        return node;
    }

    private Token Current  => _tokens[_pos];
    private Token Consume() => _tokens[_pos++];

    // or_expr = and_expr ('||' and_expr)*
    private ExpressionNode ParseOr()
    {
        var left = ParseAnd();
        while (Current.Kind == TK.Or)
        {
            Consume();
            left = new BinaryNode("||", left, ParseAnd());
        }
        return left;
    }

    // and_expr = not_expr ('&&' not_expr)*
    private ExpressionNode ParseAnd()
    {
        var left = ParseNot();
        while (Current.Kind == TK.And)
        {
            Consume();
            left = new BinaryNode("&&", left, ParseNot());
        }
        return left;
    }

    // not_expr = '!' not_expr | compare_expr
    private ExpressionNode ParseNot()
    {
        if (Current.Kind == TK.Not)
        {
            Consume();
            return new UnaryNode("!", ParseNot());
        }
        return ParseCompare();
    }

    // compare_expr = additive (cmp_op additive)?
    private ExpressionNode ParseCompare()
    {
        var left = ParseAdditive();
        var op = Current.Kind switch
        {
            TK.Eq  => "==", TK.Neq => "!=",
            TK.Gt  => ">",  TK.Lt  => "<",
            TK.Gte => ">=", TK.Lte => "<=",
            _      => null
        };
        if (op is null) return left;
        Consume();
        return new BinaryNode(op, left, ParseAdditive());
    }

    // additive = mult (('+' | '-') mult)*
    private ExpressionNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.Kind is TK.Plus or TK.Minus)
        {
            var op = Consume().Kind == TK.Plus ? "+" : "-";
            left = new BinaryNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    // mult = unary (('*' | '/') unary)*
    private ExpressionNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current.Kind is TK.Star or TK.Slash)
        {
            var op = Consume().Kind == TK.Star ? "*" : "/";
            left = new BinaryNode(op, left, ParseUnary());
        }
        return left;
    }

    // unary = '-' unary | primary
    private ExpressionNode ParseUnary()
    {
        if (Current.Kind == TK.Minus)
        {
            Consume();
            return new UnaryNode("-", ParseUnary());
        }
        return ParsePrimary();
    }

    // primary = variable | literal | '(' expr ')'
    private ExpressionNode ParsePrimary()
    {
        return Current.Kind switch
        {
            TK.Variable  => new VariableNode((string)Consume().Value!),
            TK.StringLit => new LiteralNode((string)Consume().Value!),
            TK.NumberLit => new LiteralNode((double)Consume().Value!),
            TK.BoolLit   => new LiteralNode((bool)Consume().Value!),
            TK.LParen    => ParseParen(),
            _ => throw new FormatException($"Očekáván operand, nalezeno '{Current.Kind}'.")
        };
    }

    private ExpressionNode ParseParen()
    {
        Consume(); // '('
        var inner = ParseOr();
        if (Current.Kind != TK.RParen)
            throw new FormatException("Chybí uzavírací závorka ')'.");
        Consume(); // ')'
        return inner;
    }
}
