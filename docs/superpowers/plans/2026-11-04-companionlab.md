# CompanionLab iteration 1 implementation plan

Goal: preserve Capri 0.8.2 on Windows and introduce safe, data-only character profiles.
Architecture: keep C# 5, WinForms, .NET Framework 4.8 and existing AI/voice/tools. Add a profile catalog, validated sprite overrides and a small Companion partial for menu integration. No 3D renderer or new agent engine.
Spec: the user's first-iteration requirements in this chat. Execution is authorized directly by the user without a design approval round.

## Constraints and review focus

- Preserve LICENSE.md and upstream notices; personal noncommercial fork.
- All data, process locks, startup and installer identities belong to CompanionLab.
- Never install upstream releases; keep original assets and providers available.
- Reject malformed profiles, unsupported renderers, traversal/reparse paths and invalid images before switching.
- Changing character must preserve the Brain object, model, memory and chat history; refuse while work or speech is active.
- Keep permission checks and safety prompt outside profile personality; conservative defaults, microphone off.

## Tasks

- [x] Establish original Windows compilation and smoke baseline, recording failures.
- [x] Add independent app/install identity and disable upstream updates; test packaging and defaults.
- [x] Test profile loading/validation/selection persistence, then implement CharacterProfile and CharacterCatalog in app/Characters.cs.
- [x] Integrate sprite cache invalidation, personality and voice application in app/CharacterHost.cs; add Characters menu.
- [x] Add integration smoke checks for switching, failure rollback and AI configuration/history preservation.
- [x] Run Windows compilation, full original smoke suite and focused character checks; inspect actual UI.
- [x] Write technical map, profile format, run instructions, known limitations and roadmap; commit in behavior-sized groups.

The full inherited suite is not green: baseline 528 PASS/9 FAIL, fork 598 PASS/9 FAIL. Details and remaining acceptance gaps are explicit in ITERATION-1-REPORT.md. Standalone profile checks are 26/26; focused native checks 9/9. Existing Gemma was subsequently found and verified with the real Brain headlessly, preserving original weights and history across personas. Computer-use stopped on user Escape; no GUI was restarted afterward.

## Verification

Run ci/build-windows.ps1 -SmokeTest -SmokeSeconds 900 with installed SDK metadata or explicit Microsoft.Windows.SDK.Contracts metadata directory. Character tests run through the application's smoke harness. Installer --verify only extracts/render-checks and never installs. Do not configure live providers, download model weights or enable microphone to claim an end-to-end AI check.
