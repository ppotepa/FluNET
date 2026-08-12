using FluNET.Execution.Commands;

namespace FluNET.Language;

/// <summary>Native typed-command declarations that do not require IVerb or IWord adapters.</summary>
public static class NativeModuleCommandExtensions
{
    /// <summary>Starts one native command declaration and creates its semantic frame.</summary>
    public static NativeCommandBuilder<TCommand, TResult> Command<TCommand, TResult>(
        this FluNetModuleBuilder module,
        string name,
        string usageName)
        where TCommand : class, ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(module);
        LanguageBuilder.CommandFrameBuilder frame = module.Language.CommandForRoute<TResult>(
            name,
            usageName,
            typeof(TCommand));
        return new NativeCommandBuilder<TCommand, TResult>(module, frame);
    }
}

/// <summary>Configures the syntax and semantic slots of one native typed-command frame.</summary>
public sealed class NativeCommandBuilder<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
{
    private readonly FluNetModuleBuilder _module;
    private readonly LanguageBuilder.CommandFrameBuilder _frame;
    private FrameId? _frameId;

    internal NativeCommandBuilder(
        FluNetModuleBuilder module,
        LanguageBuilder.CommandFrameBuilder frame)
    {
        _module = module;
        _frame = frame;
    }

    /// <summary>Assigns the stable identity used by planning and runtime dispatch.</summary>
    public NativeCommandBuilder<TCommand, TResult> FrameId(string id)
    {
        FrameId value = new(id);
        _frame.FrameId(value.Value);
        _frameId = value;
        return this;
    }

    /// <summary>Overrides the stable command identity shared by its frames.</summary>
    public NativeCommandBuilder<TCommand, TResult> CommandId(string id)
    {
        _frame.CommandId(id);
        return this;
    }

    /// <summary>Overrides the module identity attached to this command.</summary>
    public NativeCommandBuilder<TCommand, TResult> ModuleId(string id)
    {
        _frame.ModuleId(id);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Aliases(params string[] aliases)
    {
        _frame.Aliases(aliases);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Qualifiers(params string[] qualifiers)
    {
        _frame.Qualifiers(qualifiers);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Default()
    {
        _frame.Default();
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Positional<TValue>(
        SemanticRole role,
        SlotDirection direction = SlotDirection.Input,
        SlotCardinality cardinality = SlotCardinality.Required)
    {
        _frame.Positional<TValue>(role, direction, cardinality);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Positional<TValue>(
        FrameRoleId role,
        SlotDirection direction = SlotDirection.Input,
        SlotCardinality cardinality = SlotCardinality.Required)
    {
        _frame.Positional<TValue>(role, direction, cardinality);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Marked<TValue>(
        SemanticRole role,
        string marker,
        SlotCardinality cardinality = SlotCardinality.Required,
        SlotDirection direction = SlotDirection.Input)
    {
        _frame.Marked<TValue>(role, marker, cardinality, direction);
        return this;
    }

    public NativeCommandBuilder<TCommand, TResult> Marked<TValue>(
        FrameRoleId role,
        string marker,
        SlotCardinality cardinality = SlotCardinality.Required,
        SlotDirection direction = SlotDirection.Input)
    {
        _frame.Marked<TValue>(role, marker, cardinality, direction);
        return this;
    }

    /// <summary>Chooses the typed binder. FrameId must be declared first.</summary>
    public NativeBoundCommandBuilder<TCommand, TResult, TBinder> BindWith<TBinder>()
        where TBinder : class, ICommandBinder<TCommand, TResult>
    {
        if (_frameId is not { } frameId)
        {
            throw new LanguageDefinitionException(
                "Native commands must declare FrameId before BindWith so routing is stable.");
        }

        return new NativeBoundCommandBuilder<TCommand, TResult, TBinder>(_module, frameId);
    }
}

/// <summary>Completes a native command declaration by attaching its typed handler.</summary>
public sealed class NativeBoundCommandBuilder<TCommand, TResult, TBinder>
    where TCommand : class, ICommand<TResult>
    where TBinder : class, ICommandBinder<TCommand, TResult>
{
    private readonly FluNetModuleBuilder _module;
    private readonly FrameId _frameId;

    internal NativeBoundCommandBuilder(FluNetModuleBuilder module, FrameId frameId)
    {
        _module = module;
        _frameId = frameId;
    }

    /// <summary>Registers the typed handler and closes the native route declaration.</summary>
    public FluNetModuleBuilder HandleWith<THandler>()
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        _module.Route<TCommand, TResult, TBinder, THandler>(_frameId);
        return _module;
    }
}
