using FluNET.Compilation.Dependencies;

namespace FluNET.Execution.Planning;

public static class ExecutionPlannerDependencyExtensions
{
    /// <summary>
    /// Plans an already analyzed dependency graph. Existing planner logic remains
    /// responsible for policy parsing and result bindings; graph edges become the
    /// sole orchestration dependencies in the returned plan.
    /// </summary>
    public static ExecutionPlan Create(
        this ExecutionPlanner planner,
        DependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(graph);
        ExecutionPlan baseline = planner.Create(graph.Program.Commands, graph.Syntax);
        Dictionary<int, ExecutionPlanStep> source = baseline.Steps.ToDictionary(step => step.Index);
        ExecutionPlanStep[] steps = graph.Nodes
            .OrderBy(node => node.Index)
            .Select(node =>
            {
                ExecutionPlanStep original = source[node.Index];
                ExecutionDependency[] dependencies = graph.Incoming(node.Index)
                    .Select(ToExecutionDependency)
                    .Distinct()
                    .OrderBy(dependency => dependency.PredecessorIndex)
                    .ThenBy(dependency => dependency.Kind)
                    .ToArray();
                return new ExecutionPlanStep(
                    original.Index,
                    original.Command,
                    original.ResultBinding,
                    dependencies,
                    original.Policy);
            })
            .ToArray();
        return new ExecutionPlan(steps, baseline.Variables);
    }

    private static ExecutionDependency ToExecutionDependency(DependencyEdge edge) =>
        edge.Kind is DependencyKind.Data or DependencyKind.Condition
            ? new ExecutionDependency(edge.From, ExecutionDependencyKind.Variable, edge.Variable)
            : new ExecutionDependency(edge.From, ExecutionDependencyKind.Sequence);
}
