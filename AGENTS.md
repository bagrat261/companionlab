# CompanionLab development handoff

This is a personal noncommercial fork of lachydotmcg/capri-desktop. Read
COMPANIONLAB.md, docs/CLOUD-HANDOFF.md and docs/UPDATE-0.8.3.md before changing code.
Keep LICENSE.md, NOTICE.md and THIRD-PARTY-NOTICES.md and asset licences intact.

## Current architecture

- Windows: C# 5, WinForms, .NET Framework 4.8, native desktop interop in app/.
- app/Characters.cs validates profile manifests, personality and immutable PNG snapshots.
- app/CharacterHost.cs connects profiles to the existing Companion, Brain and voice settings.
- app/CharacterChecks.cs and tests/CharacterTests.cs cover profile regressions.
- AI provider/model/history belong to Brain, independently of the selected character.
- cross/ is upstream Avalonia/.NET 10, not a completed CompanionLab Mac port.
- quest/, site/ and worker/ are upstream components, outside the first iteration scope.

## Verification

On Windows:

```powershell
./ci/prepare-windows-metadata.ps1
./ci/build-windows.ps1 -OutputDirectory out/windows -MetadataDirectory out/sdk/contracts/ref/netstandard2.0
./ci/test-characters.ps1
./out/windows/CompanionLab.exe --smoke-test out/profile-smoke --only SmokeCharacters,SmokeCharacterRegressions
./out/windows/CompanionLab-Setup.exe --verify out/package-check
```

Check the prepare script's output path when using a fresh environment. The built-in
Framework csc is sufficient for Windows; a modern dotnet SDK does not replace it.
Linux cloud containers can build cross/ with .NET SDK 10; they cannot execute the
Windows GUI tests. Use the existing Windows/macOS GitHub Actions runners when needed.

The original v0.8.2 full smoke baseline had 528 PASS, 33 LIMIT, 9 FAIL, 13 SKIP;
the first fork baseline had 598 PASS, 34 LIMIT, 9 FAIL, 12 SKIP. These are historical,
not v0.8.3 results. Report actual failing test names and do not claim the full suite
is green. See ITERATION-1-REPORT.md for original limitations.

## Isolation and safety

Never change an installed Capri or its data. Windows identity/install/data root is
CompanionLab. Model weights may only be reused read-only with explicit checks; no
credentials, settings, conversation history, logs or model binaries belong in Git.
out/ and verification/ are ignored. Default permissions stay Guarded; microphone,
wake word, startup and integrations must not silently enable themselves.
New upstream features must preserve this boundary, including Husk discovery paths.
Keep the upstream installer/updater disabled in normal CompanionLab operation.
Discord bot integration remains future work; do not enable user-account call automation.
Do not install arbitrary mods, unverified release executables or bypass OS protection.
Preserve profile validation and the separation between personality and agent safety.
