# Contributing

Thanks for looking at TypeSafe.Sdk. This file covers the two things that catch people out: the
public API snapshot test, and what a breaking change has to carry with it.

## Getting set up

Requires the .NET 10 SDK.

```sh
dotnet restore
dotnet build
dotnet test
```

The tree builds with zero warnings and CI builds with `-warnaserror`; keep it that way rather than
suppressing a warning locally.

## The public API snapshot

`tests/TypeSafe.Sdk.Tests/PublicApiTests.cs` renders the entire public surface of `TypeSafe.Sdk`
with [PublicApiGenerator](https://github.com/PublicApiGenerator/PublicApiGenerator) and compares it,
through [Verify](https://github.com/VerifyTests/Verify), against the approved snapshot checked in
next to it:

```
tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.verified.txt
```

**Any** change to the public surface fails that test — a new public type or member, a renamed
parameter, a changed default, a new attribute on a public member, a type moving between namespaces.
That is the point: the failure is the review prompt, not a defect.

Two things are deliberately left out of the snapshot so a release does not fail it on its own:

- assembly-level attributes (`ApiGeneratorOptions.IncludeAssemblyAttributes = false`), which carry
  the assembly, file and informational versions plus `InternalsVisibleTo`;
- the *value* of the `TypeSafeConstants.Version` constant, scrubbed to `{scrubbed version}`. The
  constant itself stays in the snapshot; only its value is scrubbed, because bumping the version is
  not an API change.

### Reviewing and accepting a change

When the test fails, Verify writes what the assembly actually looks like beside the approved file:

```
tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.received.txt
```

Read the diff first — it is the changelog entry for your change, written by the compiler:

```sh
diff -u tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.verified.txt \
        tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.received.txt
```

If every line in that diff is a change you meant to make, accept it by replacing the approved file
with the received one:

```sh
mv tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.received.txt \
   tests/TypeSafe.Sdk.Tests/PublicApiTests.PublicApi.verified.txt
dotnet test --filter "FullyQualifiedName~PublicApiTests"
```

Commit the updated `.verified.txt` in the same commit as the source change, so the diff shows the
API move next to the code that made it. `*.received.*` is gitignored and must never be committed.

Verify can normally pop a [diff tool](https://github.com/VerifyTests/DiffEngine) open to review and
accept the change interactively, but this repo turns that off: the test sets
`DiffRunner.Disabled = true` in a `ModuleInitializer`, so `dotnet test` never launches an editor and
never blocks a non-interactive shell or CI — no environment variable needed either side. Review the
two files with `diff` or your editor and accept with the `mv` above.

If a line in the diff is *not* one you meant to make, that is the test doing its job — fix the code
rather than the snapshot.

## Breaking changes

A change that the snapshot shows removing or reshaping public API needs two more things before it
lands:

1. **A README entry.** Wire parity with the official Python and JavaScript SDKs is the rule for this
   port, so every deliberate divergence is listed under
   [Differences from the Python and JavaScript SDKs](README.md#differences-from-the-python-and-javascript-sdks).
   Add or update the bullet that covers your change, and say what the upstream SDKs do instead.
2. **A DECISIONS.md line.** Record the decision and its reasoning in `.project/DECISIONS.md` (the
   ProjectMan worktree — run `projectman attach` in a fresh clone to mount `.project/`), committed
   on the `projectman` branch with `/pm commit`, never from `main`.

Also bump the version in both places that hold it — `<Version>` in
`src/TypeSafe.Sdk/TypeSafe.Sdk.csproj` and `TypeSafeConstants.Version` in
`src/TypeSafe.Sdk/Constants.cs` — and run `scripts/check-version.sh` to confirm they match.

## Commits

Conventional commits (`feat:`, `fix:`, `docs:`, `test:`, …) with a body explaining why, and a `!`
or `BREAKING CHANGE:` footer when the public API moves. Branch names carry the ProjectMan task ID
(`US-TSDK-n-m`) so the commit can reference it.
