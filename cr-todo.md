# FluNET Code Review - Action Items

## Context Question
- [ ] **Decision needed:** Is FluNET staying as a lightweight embeddable "NL DSL engine + CLI" for .NET, or growing into a service (web/worker)?

---

## Phase 1: Foundation Cleanup (Days 1-3)

### Day 1: Version & Naming Standardization
- [x] Unify DI package versions across all projects
  - [x] Standardize on .NET 8 + Microsoft.Extensions.* 8.0.x (or all 9.x if needed)
  - [x] Fix Engine (8.0.0) vs CLI (9.0.10) mismatch
  - [x] Create `Directory.Build.props` to pin versions centrally

- [ ] Standardize naming to **FluNet** everywhere
  - [ ] Rename folder `FluNet.Engine` → consistent casing
  - [ ] Update all project files to use `FluNet.*` naming
  - [ ] Update all namespaces from `FluNET.*` to `FluNet.*`
  - [ ] Update NuGet package IDs

- [ ] Delete `FluNET.Syntax` project
  - [ ] Move `UserPrompt` type into Engine as `Input/` or drop entirely
  - [ ] Remove project references
  - [ ] Update solution file

### Day 2-3: Add Tests
- [ ] Create `FluNet.Tests` project (xUnit)
- [ ] Add tokenizer property tests
  - [ ] Test nested `{}` and `[]` handling
  - [ ] Test quotes and escaped brackets
  - [ ] Test trailing period logic
  - [ ] Use FsCheck for property-based testing
- [ ] Add matcher parity tests
  - [ ] Test regex vs string matcher equivalence
  - [ ] Add benchmark to pick default confidently
- [ ] Add end-to-end sentence tests
  - [ ] Golden-path E2E per verb family (GET, SAVE, DELETE, etc.)
  - [ ] Test variable resolution
  - [ ] Test THEN clause handling
- [ ] Add CLI smoke tests
  - [ ] Test parsing + routed verb
  - [ ] Test REPL commands (HELP, EXIT, CLEAR)

---

## Phase 2: Architecture Simplification (Days 4-6)

### Day 4: Consolidate Discovery/Registry/Lexicon
- [ ] Create unified **VerbCatalog** service
  - [ ] Merge functionality from DiscoveryService, VerbRegistry, and Lexicon
  - [ ] Scan verbs once on startup
  - [ ] Cache: base types, synonyms, constructor shapes, usage metadata
  - [ ] Expose `TryCreate(verb, args, IServiceProvider)` method
  - [ ] Expose `GetHelp()` and `GetUsage()` for CLI
- [ ] Delete redundant services
  - [ ] Remove DiscoveryService
  - [ ] Remove VerbRegistry
  - [ ] Remove Lexicon
  - [ ] Update all references to use VerbCatalog

### Day 5: Limit Reflection Scope
- [ ] Restrict assembly scanning
  - [ ] Limit to known assemblies (Engine + calling assemblies)
  - [ ] OR provide explicit verb registration API
  - [ ] Remove `AppDomain.CurrentDomain` broad scanning
- [ ] Add [Verb("GET", Synonyms=...)] attribute
  - [ ] Create declarative attribute for verbs
  - [ ] Let VerbCatalog build maps from attributes
  - [ ] Keep runtime discovery (no source generator needed yet)

### Day 6: Simplify Execution Pipeline
- [ ] Fold **SubSentenceExecutionStep** into SentenceExecutionStep
  - [ ] Merge THEN clause handling into main executor
  - [ ] OR treat as post-execute hook in same step
  - [ ] Reduce pipeline to 5 steps: Tokenize → Build → Validate → Execute(+Then) → VariableStore
- [ ] Update ExecutionPipelineFactory
- [ ] Update tests to reflect new pipeline structure

---

## Phase 3: Public API & DI Registration (Day 7)

### Day 7: Add Clean Registration Entrypoint
- [ ] Create `FluNetOptions` class
  - [ ] Add `UseRegex` property
  - [ ] Add `Assemblies` collection
  - [ ] Add `RegisterVerbs` callback for explicit registration
- [ ] Implement `services.AddFluNet(...)` extension method
  - [ ] Register TokenFactory/TokenTreeFactory
  - [ ] Register WordFactory
  - [ ] Register SentenceFactory/Validator/Executor
  - [ ] Register VerbCatalog
  - [ ] Register VariableResolver (as Scoped)
  - [ ] Register matchers + MatcherResolver
  - [ ] Accept configuration options
- [ ] Update CLI Program.cs to use AddFluNet()
- [ ] Remove manual DI wiring

---

## Phase 4: Hardening & Polish

### Variable Resolution
- [ ] Make VariableResolver thread-safe
  - [ ] Ensure **Scoped** lifetime registration
  - [ ] Consider ConcurrentDictionary for parallel steps
  - [ ] Define `IVariableStore` (mutable) interface
  - [ ] Define `IVariableReader` (read-only) interface
  - [ ] Make scope boundaries explicit

### Error Handling
- [ ] Introduce typed error classes
  - [ ] Create `ParseError` class
  - [ ] Create `ValidationError` class
  - [ ] Create `ExecutionError` class
- [ ] Update ExecutionResult Status enum
  - [ ] Change from generic to: `Ok | ParseError | ValidationError | RuntimeError`
- [ ] Map errors to friendly CLI messages
- [ ] Improve logging integration

### Public API Surface
- [ ] Make core types internal by default
  - [ ] Review all `public` classes
  - [ ] Keep public only for extension points: verbs, nouns, pipeline steps, option objects
  - [ ] Add XML docs for public extension points only
- [ ] Document public extension APIs

### CLI Improvements
- [ ] Handle non-interactive hosts
  - [ ] Skip color output in non-TTY
  - [ ] Detect interactive vs batch mode
- [ ] Treat HELP, EXIT, CLEAR as out-of-band commands
  - [ ] Process before calling engine
  - [ ] Add dedicated command handler

---

## Phase 5: Optional Enhancements (Stretch Goals)

### Modular Verb Packages
- [ ] Extract file I/O verbs
  - [ ] Create `FluNet.Verbs.IO` package
  - [ ] Move GET, SAVE, DELETE, LOAD verbs
- [ ] Extract HTTP verbs
  - [ ] Create `FluNet.Verbs.Http` package
  - [ ] Move DOWNLOAD, POST verbs
- [ ] Keep core engine minimal
- [ ] Document plugin architecture

### Performance & Tooling
- [ ] Add MatcherResolver benchmark
  - [ ] Compare regex vs string performance
  - [ ] Document default choice rationale
- [ ] Add performance tests
  - [ ] Measure tokenization speed
  - [ ] Measure verb lookup speed
  - [ ] Profile discovery startup time
- [ ] Consider source generator (future)
  - [ ] Evaluate for zero-reflection discovery
  - [ ] Design compile-time verb registration
  - [ ] Add as optional alternative to runtime discovery

---

## Specific File-Level Fixes

### ProcessedPrompt
- [ ] Add tests for TokenizeWithBraceAwareness
  - [ ] Test quotes handling
  - [ ] Test escaped brackets
  - [ ] Test trailing period logic
- [ ] Make trim-for-matching behavior contractual and tested
- [ ] Add XML documentation

### MatcherResolver
- [ ] Add benchmark comparing regex vs string performance
- [ ] Document when to use each matcher type
- [ ] Make default choice based on benchmark results

### CLI Program.cs
- [ ] Clean up REPL UX code
- [ ] Add non-interactive mode detection
- [ ] Separate out-of-band commands from engine calls
- [ ] Improve error message formatting

---

## Documentation Updates

- [ ] Update README with new architecture
- [ ] Document AddFluNet() registration
- [ ] Create plugin development guide
- [ ] Document testing strategy
- [ ] Add migration guide from old to new API
- [ ] Document decision to stay lightweight vs grow to service

---

## Notes

**What to Keep:**
- ✅ Pipeline architecture (clean and extensible)
- ✅ Interface composition (IVerb, IWhat/ITo/IFrom, IExecutionStep)
- ✅ MatcherResolver abstraction
- ✅ VariableResolver + brace-aware tokenization
- ✅ CLI verbs separated from app verbs

**Key Simplifications:**
- 3 projects → 2 projects (Engine + CLI)
- 3 discovery services → 1 VerbCatalog
- 6 pipeline steps → 5 pipeline steps
- Manual DI wiring → AddFluNet() registration
- Broad reflection → Limited/explicit registration

**Success Metrics:**
- [ ] All tests passing
- [ ] Zero package version mismatches
- [ ] Consistent naming across solution
- [ ] Single-line DI registration
- [ ] Clear public API surface
- [ ] Sub-100ms startup time
