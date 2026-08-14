using FluNET.Execution.Compensation;

namespace FluNET.Context;

public static class SagaExtensions
{
    public static SagaCompiler GetSagaCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SagaCompiler(context.GetCompensatableSurfaceCompiler());
    }

    public static SagaExecutor GetSagaExecutor(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SagaExecutor(
            context.GetService<FluNET.Execution.Planning.ExecutionPlanExecutor>(),
            context.GetService<FluNET.Capabilities.IFluNetFileSystem>());
    }
}
