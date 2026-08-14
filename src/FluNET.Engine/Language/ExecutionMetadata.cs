namespace FluNET.Language;

public enum ExecutionEffect { Pure, Read, Write, ExternalMutation }
public enum ConcurrencyPolicy { ParallelSafe, Ordered, Exclusive }

public sealed record FrameExecutionMetadata(FrameId FrameId, ExecutionEffect Effect, ConcurrencyPolicy Concurrency);

public interface IExecutionMetadataProvider
{
    FrameExecutionMetadata Get(CommandFrameDescriptor frame);
}

public sealed class DefaultExecutionMetadataProvider : IExecutionMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, (ExecutionEffect Effect, ConcurrencyPolicy Concurrency)> BuiltIns =
        new Dictionary<string, (ExecutionEffect, ConcurrencyPolicy)>(StringComparer.OrdinalIgnoreCase)
        {
            ["core.get.text"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["core.load.text"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["core.load.config"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.load.glob.json"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.json"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.environment"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["core.set.text"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.number"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.boolean"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.parse.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.format.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.transform.encoding"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.save.text"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.delete.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.download.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.post.json"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.send.email"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.say.text"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered)
        };

    public FrameExecutionMetadata Get(CommandFrameDescriptor frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        (ExecutionEffect effect, ConcurrencyPolicy concurrency) = BuiltIns.TryGetValue(frame.Id.Value, out var known)
            ? known
            : (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered);
        return new FrameExecutionMetadata(frame.Id, effect, concurrency);
    }
}
