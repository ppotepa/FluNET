namespace FluNET.Language;

public enum ExecutionEffect { Pure, Read, Write, ExternalMutation }
public enum ConcurrencyPolicy { ParallelSafe, Ordered, Exclusive }

public sealed record FrameExecutionMetadata(
    FrameId FrameId,
    ExecutionEffect Effect,
    ConcurrencyPolicy Concurrency);

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
            ["surface.load.csv"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.load.xml"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.load.binary"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.load.image"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.json"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.text"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.csv"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.xml"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.binary"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.http.image"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["network.http.response"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.sql"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.environment"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.get.configuration"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.environment.write"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["surface.get.secret"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.files.scan.json"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.files.list.json"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.files.stat"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.files.hash"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.info"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.metrics"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.path"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.now"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.wait"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["surface.system.notify"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["surface.system.clipboard.read"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["surface.system.clipboard.write"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["surface.system.temp.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["surface.system.temp.directory"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["surface.system.temp.cleanup"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["messaging.publish"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["messaging.receive"] = (ExecutionEffect.Read, ConcurrencyPolicy.Ordered),
            ["surface.files.copy"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["surface.files.move"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["surface.files.trash"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["storage.put.value"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["storage.read.value"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["storage.blob.get"] = (ExecutionEffect.Read, ConcurrencyPolicy.ParallelSafe),
            ["storage.blob.put"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["storage.blob.delete"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["system.process.run"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["system.process.session.start"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["system.process.session.send"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["system.process.session.stop"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.archive.create"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.archive.extract"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.directory.create"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.directory.copy"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.directory.move"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.directory.trash"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.trash.restore.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["filesystem.trash.restore.directory"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["surface.data.filter.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.sort.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.take.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.skip.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.distinct.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.project.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.default.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.group.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.sum.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.count.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.avg.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.min.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.max.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.data.join.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["surface.flow.foreach.json"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["surface.flow.while"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.set.text"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.number"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.set.boolean"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.parse.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.format.json"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.transform.encoding"] = (ExecutionEffect.Pure, ConcurrencyPolicy.ParallelSafe),
            ["core.save.text"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.save.json"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.delete.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.download.file"] = (ExecutionEffect.Write, ConcurrencyPolicy.Ordered),
            ["core.post.json"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["events.emit.webhook"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.put.json"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.patch.json"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.delete.http"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.send.email"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered),
            ["core.say.text"] = (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered)
        };

    public FrameExecutionMetadata Get(CommandFrameDescriptor frame)
    {
        var metadata = BuiltIns.TryGetValue(frame.Id.Value, out var known)
            ? known
            : (ExecutionEffect.ExternalMutation, ConcurrencyPolicy.Ordered);
        return new FrameExecutionMetadata(frame.Id, metadata.Item1, metadata.Item2);
    }
}
