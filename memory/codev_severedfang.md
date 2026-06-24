---
name: codev-severedfang
description: A teammate concurrently edits the SeveredFang boss + dialogue files; always re-read before editing to avoid duplicate classes
metadata:
  type: project
---

The SeveredFang boss encounter + dialogue system is being co-developed in real time by another person while I work.

**Why:** During the 2026-06 session I created `Data/NPC/DialogueEvents.cs` and three boss intro state files; meanwhile the teammate independently created `Data/DialogueEvents.cs` (same `Attrition.Data` namespace → duplicate class compile error), `SF_IntroState`, `SF_TelegraphState`, `BossEncounterTrigger.cs`, and added `waitForTrigger`/`introDialogue`/`StartIntroSequence` to `SeveredFangAI`. I had to delete my redundant files.

**How to apply:** Before editing anything under `Gameplay/Enemy/SeveredFang/`, `Gameplay/Environment/BossEncounterTrigger.cs`, `UI/DialogueUI.cs`, or `Data/DialogueEvents.cs`, RE-READ the current file state first — do not assume my earlier reads are current. The intro flow (trigger → walk-in → `DialogueEvents.OnOpenCustomDialogue` → combat) is THEIRS; build new systems around it, don't duplicate it.

**Known unfixed bug (their code, left untouched):** `SF_IntroState` calls `DialogueEvents.OnOpenCustomDialogue?.Invoke(...)` directly inside host-only AI logic, so in coop the CLIENT never sees the intro dialogue. Fix would be an RPC broadcast from `SeveredFangAI`. Flag before touching.
