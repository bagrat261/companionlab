# Implementation evidence

Base: Capri v0.8.2, `a5bc79592995359b347075d92233d0654f84fbe2`.

## Build baseline

- Original Windows build initially stopped because Windows SDK metadata was absent. Windows' Framework64 csc.exe was present; modern dotnet SDK, gh and Ollama were absent from PATH.
- Downloaded Microsoft.Windows.SDK.Contracts 10.0.26100.1 reference package over HTTPS from NuGet. SHA-256 `933d0aee3bf2ec5ad04d2e19d27609a95eecec4d3ff0f50b222e2e3931819ef9`.
- Added metadata-directory support and compiler response files; original application and installer then compiled, preserving upstream source behavior before the feature edits.
- Original full host smoke completed with 528 PASS / 33 LIMIT / 9 FAIL / 13 SKIP. The exact baseline log is retained separately with the delivery verification artifacts.

## Profile tests

- RED: standalone test compile failed because Characters.cs did not yet exist.
- GREEN: real disk-based loader, validation, selection and restart tests passed.
- RED: malformed PNG raised GDI+ OutOfMemoryException instead of a profile diagnostic.
- GREEN: bounded PNG-header/geometry checks and decoder error conversion; corrupt discovery no longer crashes.
- RED: replacing a validated image changed the data returned to the renderer.
- GREEN: validated compressed byte snapshots remain stable; 26 standalone assertions pass.

## Independent code review and fixes

The read-only reviewer requested changes. These were reproduced with actual code paths before fixing:

1. Raw unsafe personality default reappeared after the existing filter rejected it. RED in SmokeCharacterRegressions; GREEN after filtering the default and using a neutral fallback.
2. ResetPersonality deleted a required packaged personality.md. RED; GREEN after moving user overrides to character-settings/id/personality.md.
3. Validated sprites were reopened from mutable paths. RED in CharacterTests; GREEN with immutable data snapshots.
4. First-visit voice edits were lost and missing-profile fallback overwrote Capri's voice. Both RED; GREEN after adding explicit CharacterVoiceOwner and capturing per-character voice state on ordinary preference saves.
5. Missing permissions could be migrated to Trusted before validation, and recovery used intrusive defaults. Fixed production ordering and recovery defaults; direct missing-permission and three-start recovery assertions pass while preserving keys.
6. Installer notice verification and the tray-menu expectation still contained original text. Packaging failure was observed; expected text now matches CompanionLab and installer --verify passes.

The final focused WinForms run reports 9 PASS / 1 LIMIT / 0 FAIL / 0 SKIP. It checks actual custom PNG usage, local TTS routing, nonempty chat/model/provider/memory preservation, busy rejection, fallback, profile reset, voice ownership and fresh/recovery defaults. The final test-only addition invokes the actual menu item's click handler, confirms Luna is selected, and captures the native submenu. TTS audio and external cloud LLMs remain untested.

## Isolation and packaging

- Separate EXE/product/manifest, install marker, uninstall/startup registry names, mutex/events and input-release mapping namespace.
- Data root and speech-engine plain-path alias belong to CompanionLab. Default saved clips use Videos/CompanionLab Clips. Self-knowledge points to the fork's data.
- Upstream update entrypoints return a manual-update explanation in normal mode; upstream updater regression tests remain available only under smoke mode.
- Build ships and embeds LICENSE.md, NOTICE.md and the font OFL. Installer verification extracts them; extracted application SHA-256 equals the built application.
- No install/uninstall, Defender changes, user-account Discord automation, messages, keys or model-weight downloads were performed.
- A run from the restrictive native shell sandbox produced 408 PASS / 27 LIMIT / 50 FAIL / 13 SKIP, including loopback/network/temp-file failures. The final comparison uses the same Git Bash host route as the original baseline: 598 PASS / 34 LIMIT / 9 FAIL / 12 SKIP. The final binary subsequently added only the menu-click/screenshot test and was recompiled and checked with focused tests and packaging again.

## Existing local Gemma

After the user clarified that Capri was already installed, a read-only check found the Desktop shortcut, Gemma 4 E4B Q4_0 and its projector. Both weight hashes matched Engine.cs. The original app and model server were not running. A separate copy of the official 32,182,350-byte llama.cpp b11669 archive was downloaded and its pinned SHA-256 verified before execution. No installed Capri executable was launched.

The actual Brain class from CompanionLab.exe answered in Russian as Capri, then Luna, through that single local model. History grew from 2 to 4 and the model/provider remained the same. The test server was stopped in finally; VRAM returned to its prior 2729 MiB used / 9215 MiB free. Original weight hashes remained unchanged. See verification/live-brain.json. The reusable launcher is ci/run-existing-gemma.ps1; its ProbeOnly path is tested, and its GUI branch was not run after the user stopped computer-use with Escape.

## Final evidence location

See ITERATION-1-REPORT.md and the sibling delivery verification directory for final full-smoke totals and manual UI observations. This evidence records the implementation checks; it does not label every upstream subsystem production-ready.
