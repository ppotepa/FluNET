# FluNET

**Author:** Paweł Potępa

FluNET is an experimental C#/.NET project that lets you express programs as English‑like commands instead of traditional method calls. Sentences such as:

```text
GET [text] FROM file.txt
SAVE [content] TO output.txt
DOWNLOAD [data] FROM https://api.example.com
```

are parsed, tokenized, and executed by a small engine that understands verbs, nouns, and prepositions. Under the hood, FluNET builds a token tree, resolves verbs via reflection, and invokes strongly‑typed command objects, giving you natural‑language style syntax without losing type safety, composability, or testability.

The project serves as a playground for language design, internal DSLs, and metaprogramming in the .NET ecosystem.
