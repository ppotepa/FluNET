namespace FluNET.Syntax.Core;

/// <summary>
/// Direction of data flow represented by a semantic sentence role.
/// </summary>
public enum RoleDirection
{
    Input,
    Output,
    InputOutput
}

/// <summary>
/// Marker for semantic roles such as WHAT, FROM, TO, USING and WITH.
/// </summary>
public interface IRole
{
}

/// <summary>
/// Strongly typed semantic role. The CLR type describes the value shape while
/// constructor metadata describes syntactic occurrence/cardinality.
/// </summary>
public interface IRole<out TValue> : IRole
{
}
