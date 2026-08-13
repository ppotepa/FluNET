using FluNET.Language;
using FluNET.Language.Binding;
using System.Collections.ObjectModel;

namespace FluNET.Compilation;

/// <summary>A typed command value produced once during compilation.</summary>
public sealed record CompiledCommand
{
    public CompiledCommand(
        BoundCommand source,
        object value,
        Type commandClrType,
        Type resultClrType,
        TypeSymbol resultType)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        CommandClrType = commandClrType ?? throw new ArgumentNullException(nameof(commandClrType));
        ResultClrType = resultClrType ?? throw new ArgumentNullException(nameof(resultClrType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    public BoundCommand Source { get; }
    public FrameId FrameId => Source.Frame.Id;
    public CommandId CommandId => Source.Command.Id;
    public object Value { get; }
    public Type CommandClrType { get; }
    public Type ResultClrType { get; }
    public TypeSymbol ResultType { get; }
}

/// <summary>Canonical compile-time IR consumed by type checking and planning.</summary>
public sealed class TypedProgram
{
    private readonly ReadOnlyCollection<CompiledCommand> _commands;

    public TypedProgram(BoundProgram boundProgram, IEnumerable<CompiledCommand> commands)
    {
        BoundProgram = boundProgram ?? throw new ArgumentNullException(nameof(boundProgram));
        _commands = Array.AsReadOnly(commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands)));
        if (_commands.Count != BoundProgram.Commands.Count)
        {
            throw new ArgumentException("Typed and bound command counts must match.", nameof(commands));
        }
    }

    public BoundProgram BoundProgram { get; }
    public FluNetProgram Program => BoundProgram.Program;
    public IReadOnlyList<CompiledCommand> Commands => _commands;
}
