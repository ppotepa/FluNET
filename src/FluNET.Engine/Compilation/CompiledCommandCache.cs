using FluNET.Language.Binding;
using System.Runtime.CompilerServices;

namespace FluNET.Compilation;

internal static class CompiledCommandCache
{
    private static readonly ConditionalWeakTable<BoundCommand, Holder> Items = new();

    public static bool TryGet(BoundCommand source, out CompiledCommand? command)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Items.TryGetValue(source, out Holder? holder))
        {
            command = holder.Command;
            return true;
        }
        command = null;
        return false;
    }

    public static CompiledCommand Set(BoundCommand source, CompiledCommand command)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(command);
        Items.Remove(source);
        Items.Add(source, new Holder(command));
        return command;
    }

    private sealed record Holder(CompiledCommand Command);
}
