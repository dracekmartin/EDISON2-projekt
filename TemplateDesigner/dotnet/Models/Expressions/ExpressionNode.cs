namespace TemplateDesigner.Models.Expressions;

/// <summary>Uzel AST výrazu.</summary>
public abstract record ExpressionNode;

/// <summary>Literální hodnota - string, double nebo bool.</summary>
public record LiteralNode(object? Value) : ExpressionNode;

/// <summary>Odkaz na proměnnou z DataContext, např. {TISK_SIPKY}.</summary>
public record VariableNode(string Name) : ExpressionNode;

/// <summary>Unární operátor - aktuálně pouze '!'.</summary>
public record UnaryNode(string Op, ExpressionNode Operand) : ExpressionNode;

/// <summary>Binární operátor - porovnání nebo logický operátor.</summary>
public record BinaryNode(string Op, ExpressionNode Left, ExpressionNode Right) : ExpressionNode;
