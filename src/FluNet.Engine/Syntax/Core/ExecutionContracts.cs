namespace FluNET.Syntax.Core;

public interface IAsyncVerb : IVerb
{
    ValueTask<object?> InvokeAsync(CancellationToken cancellationToken = default);
}

public interface IPureOperation { }
public interface IIdempotentOperation { }
public interface IRetryableOperation { }
public interface ITransactionalOperation { }
public interface ILongRunningOperation { }
public interface ISideEffectingOperation { }

public interface IPipelineProducer<out T> { }
public interface IPipelineConsumer<in T> { }
