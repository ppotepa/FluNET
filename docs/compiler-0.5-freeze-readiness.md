# FluNET 0.5 compact-language freeze readiness

This is a source-level milestone record, not a release claim. Public `StandardLanguageIdentity.Version` remains `0.3` until the exact candidate tree passes Release verification.

## Implemented compact surface

- `SourceDocument` / `SurfaceProgramSyntax` front end;
- direct structural lowering to canonical `PromptSyntax` with `SourceMap`;
- deterministic resource/format/type/name inference;
- compact `LOAD` and `GET`;
- lexical `FROM`, named `USE`, retry and timeout directives;
- property/index paths and interpolation;
- dependency/effect analysis rather than source-order serialization;
- explicit `|` pipelines and implicit multiline data flow;
- `,` as a same-role coordination separator;
- `;` and newline as neutral statement boundaries;
- `flunet check`, `fmt`, `explain`, `graph`, and `run`.

## Separator contract

```text
,        more of the same syntactic role
;        neutral next statement
newline  neutral next statement
|        data flow
AND      explicit canonical parallel coordination
THEN     explicit canonical ordering/barrier
```

Neither comma nor semicolon creates an execution dependency by itself.

## Freeze gate

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```
