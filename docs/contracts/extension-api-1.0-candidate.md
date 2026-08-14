# FluNET 1.0 extension API candidate

`ExtensionApiContractManifest.Candidate1_0` is the source-level ledger for APIs that third-party modules should build against.

Candidate stable boundaries:

- `IFluNetModule` / `FluNetModuleBuilder`;
- typed `ICommand<T>`, `ICommandBinder<TCommand,TResult>`, `ICommandHandler<TCommand,TResult>`;
- `IValueCodec<T>` and `IValueConversion<TSource,TTarget>`;
- `IResourceProvider`, `IResourceDecoder`, `IResourceEncoder`, `IResourceObserver`;
- host capability interfaces for execution policy, secrets, HTTP/auth and SQL.

The contract freezes responsibilities and generic shapes, not internal implementation classes. Modules should depend on stable `CommandId`/`FrameId`/`TypeId` and these interfaces rather than reflection over CLR class names.

This remains `1.0-candidate`; the public language identity is not changed until verification.
