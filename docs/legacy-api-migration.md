# Legacy API migration for FluNET 0.3

FluNET 0.3 separates the canonical compiler/runtime from the original word-chain model.

## Canonical path

New code should use:

- `Engine.Analyze(ProcessedPrompt)` for side-effect-free parse, bind, validation, and planning;
- `CompilationResult.Program`, `BoundProgram`, `DiagnosticBag`, and `Plan` for compiler output;
- `Engine.Execute` or `Engine.ExecuteAsync` for execution;
- `FluNetModuleBuilder.Command<TCommand, TResult>()` with `FrameId`, semantic slots, `BindWith<TBinder>()`, and `HandleWith<THandler>()` for new modules.

`ExecuteAsync` follows one path:

```text
Parse -> Bind -> Validate -> Plan -> Execute
```

It does not construct `TokenTree`, run `SentenceValidator`, or create `ISentence`.

## Compatibility path

`ISentence`, `Engine.Run`, and the old manually assembled `Engine` constructor are deprecated compatibility APIs. `LegacySentenceAdapter` is the only bridge from canonical prompt syntax to the old `TokenTree`/`IWord`/`ISentence` representation.

The adapter performs compatibility validation and projection only. It never invokes a command handler and it never participates in `ExecuteAsync`.

Native typed modules may have no `ISentence` representation at all. That does not make their `CompilationResult` invalid; use `CompilationResult.IsCompilationSuccessful` and the typed plan instead.

## Module migration

Legacy declaration:

```csharp
language.Command<MyVerb, string>("GENERATE", "Report")
    .Positional<string>(SemanticRole.Output, SlotDirection.Output);
```

Native declaration:

```csharp
module.Command<GenerateReportCommand, string>("GENERATE", "Report")
    .FrameId("reporting.generate")
    .Positional<string>(SemanticRole.Output, SlotDirection.Output)
    .BindWith<GenerateReportBinder>()
    .HandleWith<GenerateReportHandler>();
```

The native command, binder, and handler do not inherit from `IVerb` or `IWord`, do not implement `Invoke()`, and are dispatched by stable `FrameId` rather than CLR verb type.

## Compatibility timeline

The legacy API remains available during the 0.x migration period. It should be treated as read-only compatibility surface; new validation, planning, execution, and module features are added only to the canonical typed pipeline.
