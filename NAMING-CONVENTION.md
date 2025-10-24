# FluNET Naming Convention

## Project Standard: FluNET (All Caps NET)

**Rule:** All project names, namespaces, and assemblies MUST use `FluNET` (with capital N-E-T) to maintain the ".NET library" branding feel.

## Rationale

- **FluNET** conveys ".NET library" identity
- Consistent with .NET ecosystem naming (ASP.NET, ADO.NET, etc.)
- Distinguishes from generic "fluent" libraries

---

## Naming Rules

### ✅ Correct Naming

| Element | Pattern | Example |
|---------|---------|---------|
| Namespace | `FluNET.*` | `FluNET.Engine`, `FluNET.CLI`, `FluNET.Syntax` |
| Project | `FluNET.*.csproj` | `FluNET.Engine.csproj`, `FluNET.CLI.csproj` |
| Directory | `FluNET.*` | `src/FluNET.Engine/`, `tests/FluNET.Tests/` |
| Assembly | `FluNET.*` | `FluNET.Engine.dll`, `FluNET.CLI.exe` |
| NuGet Package | `FluNET.*` | `FluNET.Engine`, `FluNET.Verbs.IO` |
| Root Namespace | `FluNET` | `<RootNamespace>FluNET</RootNamespace>` |

### ❌ Incorrect Naming

- ❌ `FluNet.Engine` (lowercase 'net')
- ❌ `Flunet.Engine` (lowercase 'net')
- ❌ `flunet.Engine` (lowercase start)
- ❌ `FluNET.engine` (lowercase component)

---

## Enforcement

### 1. EditorConfig (`.editorconfig`)

The `.editorconfig` file enforces namespace naming at the IDE level:

```ini
# Namespaces must start with FluNET
dotnet_naming_rule.namespace_must_start_with_flunet.severity = error
dotnet_naming_rule.namespace_must_start_with_flunet.symbols = namespace_symbols
dotnet_naming_rule.namespace_must_start_with_flunet.style = flunet_namespace_style

dotnet_naming_symbols.namespace_symbols.applicable_kinds = namespace
dotnet_naming_style.flunet_namespace_style.required_prefix = FluNET
dotnet_naming_style.flunet_namespace_style.capitalization = pascal_case
```

### 2. Directory.Build.props

Centralizes root namespace across all projects:

```xml
<PropertyGroup>
  <RootNamespace>FluNET</RootNamespace>
</PropertyGroup>
```

### 3. Project Files (*.csproj)

Each project explicitly sets:

```xml
<PropertyGroup>
  <RootNamespace>FluNET</RootNamespace>
  <AssemblyName>FluNET.Engine</AssemblyName>
  <PackageId>FluNET.Engine</PackageId>
</PropertyGroup>
```

### 4. Visual Studio / VS Code

- **Visual Studio 2022:** Respects `.editorconfig` naming rules, shows errors for violations
- **VS Code:** With C# extension, highlights naming convention violations
- **Build warnings:** Can be promoted to errors via MSBuild properties

---

## Examples

### Namespace Declaration

```csharp
// ✅ Correct
namespace FluNET.Engine;
namespace FluNET.Engine.Syntax.Verbs;
namespace FluNET.CLI.Verbs;

// ❌ Incorrect
namespace FluNet.Engine;  // lowercase 'net'
namespace Flunet.Engine;  // lowercase 'net'
```

### Using Statements

```csharp
// ✅ Correct
using FluNET.Engine;
using FluNET.Engine.Syntax;

// ❌ Incorrect
using FluNet.Engine;
```

### Type Names

```csharp
// ✅ Correct
public class FluNETEngine { }
public interface IFluNETContext { }

// Note: Internal type names can vary, but namespace MUST be FluNET.*
```

---

## Migration Checklist

When creating new projects or files:

- [ ] Namespace starts with `FluNET`
- [ ] Project file name is `FluNET.*.csproj`
- [ ] Directory name is `FluNET.*`
- [ ] `<RootNamespace>FluNET</RootNamespace>` in `.csproj`
- [ ] `<AssemblyName>FluNET.*</AssemblyName>` in `.csproj`
- [ ] `<PackageId>FluNET.*</PackageId>` for NuGet packages

---

## Future Packages

All extension packages must follow the naming convention:

- `FluNET.Verbs.IO` (file I/O verbs)
- `FluNET.Verbs.Http` (HTTP verbs)
- `FluNET.Verbs.Database` (future)
- `FluNET.Extensions.Logging` (future)

---

## References

- `.editorconfig` - IDE-level enforcement
- `Directory.Build.props` - MSBuild-level defaults
- This document - Team reference and onboarding
