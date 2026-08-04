# Save payload format

Reference for the data the game pushes to the web backend and reads back. Written 2026-08-04, when
the co-op save was consolidated from 3–4 requests per save into one.

Two save paths exist and they are **not** interchangeable:

| | SOLO | CO-OP (online) |
|---|---|---|
| Destination | local slot file on disk | `POST /api/sessions/bulk` |
| Written by | `SaveManager` → `SaveSlotData` | `GameSaveService.SaveAllOnline` |
| Scope | one player | the whole party, host writes for everyone |
| World progress | inside the slot file | room-level rows on the server |

This document covers the **co-op** payload. Solo saves use `SaveSlotData` and never leave the machine.

---

## Endpoint

```
POST /api/sessions/bulk        Authorization: host's JWT (cookie)
```

One request per save. The server applies every row in a **single transaction** — if any part fails,
nothing is written, so the two players can never end up at different points in the story.

Two independent ownership checks apply:

1. **Room** — `SessionController.RequireOwnership`: the caller must own the room, else `403`.
2. **Character** — `SessionService.BulkSaveAsync`: for each entry, the server looks up `characterId`
   in the `characters` table and requires `OwnerId == ownerId` from the payload. A mismatch puts that
   id in the response's `skipped` list and writes nothing for it; the rest of the save still commits.

The second check is what lets the host write the *client's* progress without being able to forge rows
for a character they don't own. Before this, the server derived the owner from the host's JWT, so
only the host's data was ever recorded and the co-op partner's progress never appeared on the web.

Duplicate `characterId` / `eventId` entries are deduped before writing (last one wins).

---

## Request: `BulkSaveRequest`

Unity mirror: `APIManager.BulkSaveRequest` (camelCase). Server: `DTOs/SessionDtos.cs` (PascalCase).

| Field | Type | Meaning | Source |
|---|---|---|---|
| `sessionId` | guid | Room being saved. Required. | `GameLaunch.SessionId` |
| `playTimeSeconds` | int | Total playtime for the room. | `GameSaveService.TotalPlaytimeSeconds` |
| `currentScene` | string? | Map the party is on. `null` = keep existing. | `GameLaunch.GameplayScene` |
| `fogJson` | string? | JSON array of revealed fog cells. `null` = keep existing. | `WorldMapState.AllFog` |
| `eventType` | string | What triggered the save: `"rest"` \| `"quit"`. | `SaveEvent` enum, lowercased |
| `roomCode` | string? | Join code, recorded on each snapshot. Falls back to the stored code. | `GameLaunch.RoomCode` |
| `characters` | list | One entry per player present. | `FindObjectsByType<PlayerController>` |
| `worldStates` | list | Room-level flags (see below). | `BossDefeatState`, `WorldMapState`, `NetworkNPC` |

`currentScene` must come from `GameLaunch.GameplayScene`. `SceneManager.GetActiveScene()` returns the
menu scene in co-op (maps load additively) and `gameObject.scene` returns Fusion's runner scene.

### `characters[]` — `BulkCharacterDto`

One row per player, keyed `(characterId, sessionId)`. Stats live per room, so the same character can
be level 12 in one room and level 3 in another.

| Field | Type | Meaning | Source |
|---|---|---|---|
| `characterId` | guid | Which character. Verified against `characters`. | `PlayerInventory.OwnerCharacterId` |
| `ownerId` | guid | Which user owns it. Verified, not trusted. | `PlayerInventory.OwnerUserId` |
| `playerRole` | short | `0` = host, `1` = joining client. | `Object.HasInputAuthority` |
| `name` | string | Display name; `"Wanderer"` if unset. | `PlayerController.DisplayName` |
| `archetype` | string | Always `"default"` — no class system yet. | hardcoded |
| `currentLevel` | int | Level. | `PlayerProgression.Level` |
| `currentExp` | int | Exp toward the next level. | `PlayerProgression.CurrentExp` |
| `allocatedPointsJson` | string? | JSON `int[7]`, see below. `null` = keep existing. | `PlayerStats.AllocatedPoints` |
| `maxHp` `currentHp` | int | Health. | `PlayerStats` |
| `maxMana` `currentMana` | int | Mana. | `PlayerStats` |
| `maxStamina` | int | Stamina cap (current stamina regenerates, so it isn't saved). | `PlayerStats` |
| `potionMaxFlasks` | int | Health flask **capacity** (upgraded at rest points). | `PotionSystem.MaxHealthCharges` |
| `potionMaxManaFlasks` | int | Mana flask capacity. | `PotionSystem.MaxManaCharges` |
| `healthCharges` | int | Health flasks **remaining**. | `PotionSystem.HealthCharges` |
| `manaCharges` | int | Mana flasks remaining. | `PotionSystem.ManaCharges` |
| `attackSpeed` | float | Attack speed multiplier. | `PlayerStats.AttackSpeed` |
| `ad` `ap` `def` `res` | int | **Final** combat stats, see below. | `PlayerStats.AD/AP/DEF/RES` |
| `posX` `posY` `posZ` | float | Where the character stood. | rest point (host) or live transform |
| `lastRestPointId` | string? | Respawn point. `null` = keep existing. | `Checkpoint.DisplayName` |
| `inventoryJson` | string? | Inventory blob, see below. `null` = keep existing. | `PlayerInventory.ExportJson()` |
| `equipmentJson` | string? | **Always `null`** — equipped gear rides inside `inventoryJson`. | — |
| `deathCount` | int | Deaths in this room. Does not reset on revive. | `PlayerStats.DeathCount` |
| `isAlive` | bool | Alive at save time; recorded on the snapshot only. | `!PlayerController.IsDead` |

Identity (`characterId` / `ownerId`) must be read from the networked `PlayerInventory` fields. The
`GameLaunch` statics are host-local, so using them attributes both players to the host.

`checkpointId` and `checkpointPos` describe where the **host** rested, so they are applied only to
the host's row; the client keeps its own position.

Flask charges were previously not persisted. Old rows have `0`/`0`, which `HydrateFromCoopSession`
treats as "unknown" and refills to full — matching the pre-existing behaviour rather than spawning
the party with empty flasks.

`ad`/`ap`/`def`/`res` are the **already-combined** values (base + allocated points + equipped gear).
`PlayerStats` exposes them as computed properties over the active stat sheet and never persists them,
and the web has no gear stat table, so it cannot recompute them from `allocatedPointsJson` — the only
way to display them is to store what the client computed. This makes them a **snapshot, not a
source**: they are never read back into the game (`PlayerStats` recomputes on spawn), so a stale or
wrong value can only mis-render the web page, never corrupt a character. Rows saved by a build older
than this field are all-zero; the room page treats all-zero as "unknown" and hides the row rather
than displaying a fake `0`.

### `worldStates[]` — `BulkWorldStateDto`

Room-level progress, keyed `(sessionId, eventId)`. All three kinds share one table, so `eventId`
carries a prefix. Keep these in sync with `GameSaveService` (C#) and `lib/world-state.ts` (web);
the column is `varchar(50)`, hence the terse prefixes.

| `eventId` | Kind | `stateValue` | `progress` |
|---|---|---|---|
| `q:<questId>` | Quest | quest state | counter |
| `cp:<pointId>` | Rest point discovered | `1` | unused |
| `<bossId>` | Boss defeated (no prefix) | `1` | unused |

A boss id must not begin with `q:` or `cp:`. Ids come from `EnemyStats.EnemyId` (e.g.
`severed_fang`), so this holds — but a boss called `queen_moth` is safe precisely because the check
is for `q:` and not `q`.

---

## Response: `BulkSaveResultDto`

| Field | Type | Meaning |
|---|---|---|
| `sessionId` | guid | Echo of the room saved. |
| `charactersSaved` | int | Rows actually written. |
| `worldStatesSaved` | int | Flags written (deduped count). |
| `skipped` | guid[] | Characters rejected for failing the ownership check. |

`200` with a non-empty `skipped`, or with `charactersSaved == 0` when characters were sent, is a
**failure** from the player's perspective — the client logs it and shows the error toast rather than
"Progress saved."

---

## Blob formats

### `inventoryJson`

Produced by `PlayerInventory.ExportJson()`. Slot position is encoded **by list index**: every slot is
written including empty ones, so entry `i` is bag slot `i`.

```jsonc
{
  "equipmentSlots":  [ { "itemId": "iron_sword", "amount": 1 }, { "itemId": "", "amount": 0 }, ... ],  // 40
  "accessorySlots":  [ ... ],   // 10
  "materialSlots":   [ ... ],   // 14
  "equippedHead":    { "itemId": "leather_cap", "amount": 1 },
  "equippedChest":   { ... },
  "equippedLegs":    { ... },
  "equippedBoots":   { ... },
  "equippedSkill":   { ... },
  "equippedAccessory": { ... }
}
```

An empty slot is `{ "itemId": "", "amount": 0 }`. Capacities mirror the `[Capacity(n)]` attributes on
the networked arrays; anything past the end is ignored, anything missing reads back empty.

This positional encoding is fragile: changing a bag's capacity, or filtering empties out anywhere
along the way, shifts every subsequent item. The web renderer must index into a fixed-size grid
rather than compacting the list. See the `// ponytail:` note at `SerializeArray` — the upgrade path
is an explicit `slotIndex` field with positional fallback for old saves.

### `allocatedPointsJson`

JSON `int[7]` of self-allocated stat points, indexed by the first seven entries of `StatType`:

```
[ MaxHP, MaxMana, MaxStamina, AD, AP, DEF, RES ]
```

`MoveSpeed` and `AttackSpeed` exist in the enum but aren't allocatable, so they aren't in the array.

### `fogJson`

JSON array of `"<scene>:<cellX>:<cellY>"` keys, one per revealed cell — room-level, since the party
explores one shared map. Scene names can contain spaces and dashes (`"Elf Valley -Map 3"`), so parse
the coordinates from the **right**.

---

## Derived, never stored

- **`unspentPoints`** = `(level - 1) × 5 − Σ allocatedPoints`, clamped at 0. `5` is
  `LevelingConfigSO.statPointsPerLevel`. Storing it would let it drift from the allocation array.
- **Fog cell counts per map** — counted from `fogJson` at render time.

`ad`/`ap`/`def`/`res` are the exception that proves the rule: they *are* derived in the game, but the
web lacks the inputs (base sheet + gear stats) to derive them, so they are transmitted instead. See
the note under `characters[]` — they are display-only and never read back.

## Deliberately absent

- **`gold`** — the snapshot column exists and is always written as `0`; the game has no gold system.
- **`equipmentJson`** — always `null` from Unity; equipped gear is inside `inventoryJson`.
- **`slotIndex`** — position is positional, as described above.
- **current stamina** — regenerates on its own; only the cap is meaningful across saves.

---

## Reading it back

`GET /api/sessions/{id}` returns the whole room in **one** request: `SessionDetailDto` with every
character's progress, all world-state rows and `fogJson`. The load path is single-request by design —
`PlayerInventory.EnsureSessionLoaded` fetches once and caches into `GameLaunch`, and every other
component reads that cache.

On load the game restores, in addition to stats and inventory:

- `BossDefeatState.LoadFromIds` — defeated bosses, so they don't respawn at full health.
- `WorldMapState.LoadFromCoop` — fog and discovered rest points.
- `Checkpoint.ApplyCoopDiscovered` — re-lights rest-point beacons whose `Spawned()` ran before the
  fetch returned.
- `BossGateController.FixedUpdateNetwork` — re-checks each tick for the same ordering reason, and
  only ever marks a boss defeated (never revives one).

The server response also carries `name` and `archetype` joined from the `characters` table, since the
session row stores only `characterId`.

## Legacy endpoints

`POST /api/sessions/character`, `POST /api/sessions/meta` and `POST /api/characters/snapshot` still
exist and still work — already-shipped game builds use them. `GameSaveService` no longer calls the
first two. `PostSnapshot` is still used by character creation in `MainMenuUIController`.
