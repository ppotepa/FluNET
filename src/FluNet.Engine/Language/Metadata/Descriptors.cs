using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Language.Metadata;

public sealed record ParameterDescriptor(
    ParameterInfo Parameter,
    string Name,
    Type ParameterType,
    ClauseKind? Role,
    RoleDirection Direction,
    bool IsOptional,
    bool IsParams,
    bool FromServices,
    NullabilityState ReadState,
    NullabilityState WriteState,
    TypeShape Shape);

public sealed record ConstructorDescriptor(
    ConstructorInfo Constructor,
    IReadOnlyList<ParameterDescriptor> Parameters)
{
    public int RoleParameterCount => Parameters.Count(x => x.Role != null);
    public int ServiceParameterCount => Parameters.Count(x => x.FromServices);
}
