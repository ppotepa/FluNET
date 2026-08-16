namespace FluNET.Language.Metadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
public sealed class VerbAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class RoleAttribute(ClauseKind kind) : Attribute
{
    public ClauseKind Kind { get; } = kind;
}

public sealed class WhatAttribute : RoleAttribute { public WhatAttribute() : base(ClauseKind.What) { } }
public sealed class FromAttribute : RoleAttribute { public FromAttribute() : base(ClauseKind.From) { } }
public sealed class ToAttribute : RoleAttribute { public ToAttribute() : base(ClauseKind.To) { } }
public sealed class UsingAttribute : RoleAttribute { public UsingAttribute() : base(ClauseKind.Using) { } }
public sealed class WithAttribute : RoleAttribute { public WithAttribute() : base(ClauseKind.With) { } }
public sealed class ThenAttribute : RoleAttribute { public ThenAttribute() : base(ClauseKind.Then) { } }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class OptionalRoleAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class InputAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class OutputAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class InputOutputAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromServicesAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class AliasAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class QualifierAttribute(string text) : Attribute
{
    public string Text { get; } = text;
    public Type? ValueType { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class RequiresCapabilityAttribute(string capability) : Attribute
{
    public string Capability { get; } = capability;
}
