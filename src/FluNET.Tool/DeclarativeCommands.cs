using FluNET.Context;
using FluNET.Declarative;
using FluNET.Declarative.Reconciliation;

public static partial class FluNetTool
{
    private static async Task<int> EnsureAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3)
            return UsageError("Usage: flunet ensure check|apply FILE");

        string source = await ReadSourceAsync(args[2]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        DesiredStateCompilationResult compilation = context.CompileEnsure(source);
        if (!compilation.IsValid)
        {
            foreach (DesiredStateDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }

        if (args[1].Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Valid: {compilation.Plans.Count} ENSURE goal(s).");
            return 0;
        }

        if (args[1].Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync(source);
            foreach (EnsureRunResult run in runs)
                Console.WriteLine($"{run.Plan.Goal.Target}: {(run.IsSuccess ? "satisfied" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }

        return UsageError($"Unknown ensure action '{args[1]}'.");
    }

    private static async Task<int> SyncAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3)
            return UsageError("Usage: flunet sync check|apply FILE");

        string source = await ReadSourceAsync(args[2]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult compilation = context.CompileSync(source);
        if (!compilation.IsValid)
        {
            foreach (SyncDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }

        if (args[1].Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Valid: {compilation.Definitions.Count} SYNC definition(s).");
            return 0;
        }

        if (args[1].Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<ReconciliationRunResult> runs = await context.ExecuteSyncAsync(source);
            foreach (ReconciliationRunResult run in runs)
            {
                string status = run.IsSuccess
                    ? run.Applied ? "applied" : "unchanged"
                    : "failed";
                Console.WriteLine($"{run.Definition.Goal.TargetResource}: {status}");
                if (run.Diff is not null)
                {
                    Console.WriteLine(
                        $"  create={run.Diff.Creates} update={run.Diff.Updates} " +
                        $"delete={run.Diff.Deletes} conflict={run.Diff.Conflicts}");
                }
            }
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }

        return UsageError($"Unknown sync action '{args[1]}'.");
    }
}
