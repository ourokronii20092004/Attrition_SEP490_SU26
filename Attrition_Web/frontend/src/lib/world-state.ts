/**
 * World-state rows for a co-op room all live in one table, so the game namespaces them by an
 * eventId prefix. This mirrors `GameSaveService` on the Unity side (QuestPrefix/CheckpointPrefix):
 *
 *   "q:<questId>"   quest progress   — stateValue is the quest state, progress the counter
 *   "cp:<pointId>"  discovered rest point (stateValue 1)
 *   "<bossId>"      defeated boss, no prefix (stateValue 1)
 *
 * Keep the prefixes in sync with the C# constants; they're short because the column is varchar(50).
 */
import type { WorldStateDto } from "./types";

const QUEST_PREFIX = "q:";
const CHECKPOINT_PREFIX = "cp:";

export interface RoomProgress {
  quests: { id: string; state: number; progress: number }[];
  checkpoints: string[];
  bosses: string[];
}

export function splitWorldStates(states: WorldStateDto[] | null | undefined): RoomProgress {
  const out: RoomProgress = { quests: [], checkpoints: [], bosses: [] };
  for (const ws of states ?? []) {
    if (!ws?.eventId) continue;

    if (ws.eventId.startsWith(QUEST_PREFIX)) {
      out.quests.push({
        id: ws.eventId.slice(QUEST_PREFIX.length),
        state: ws.stateValue,
        progress: ws.progress,
      });
    } else if (ws.eventId.startsWith(CHECKPOINT_PREFIX)) {
      if (ws.stateValue > 0) out.checkpoints.push(ws.eventId.slice(CHECKPOINT_PREFIX.length));
    } else if (ws.stateValue > 0) {
      out.bosses.push(ws.eventId);
    }
  }
  return out;
}

/** Fog cells are stored as a JSON array of "scene:cellX:cellY". Returns cells grouped by scene. */
export function parseFog(fogJson: string | null | undefined): Map<string, number> {
  const byScene = new Map<string, number>();
  if (!fogJson) return byScene;

  let cells: unknown;
  try {
    cells = JSON.parse(fogJson);
  } catch {
    return byScene;
  }
  if (!Array.isArray(cells)) return byScene;

  for (const cell of cells) {
    if (typeof cell !== "string") continue;
    // Key is scene:cx:cy with cx/cy integers, so the scene is everything before the last two
    // segments — split from the right in case a scene name itself contains a colon.
    const parts = cell.split(":");
    if (parts.length < 3) continue;
    const scene = parts.slice(0, -2).join(":");
    byScene.set(scene, (byScene.get(scene) ?? 0) + 1);
  }
  return byScene;
}

/**
 * Self-allocated stat points, in `StatType` order. The game grants
 * `statPointsPerLevel` (5) per level after the first, so unspent is derived, never stored.
 */
export const STAT_LABELS = ["Max HP", "Max Mana", "Max Stamina", "AD", "AP", "DEF", "RES"] as const;

const POINTS_PER_LEVEL = 5;

export function parseAllocated(json: string | null | undefined): number[] {
  if (!json) return [];
  try {
    const arr = JSON.parse(json);
    return Array.isArray(arr) ? arr.map((n) => (typeof n === "number" ? n : 0)) : [];
  } catch {
    return [];
  }
}

export function unspentPoints(level: number, allocated: number[]): number {
  const spent = allocated.reduce((a, b) => a + b, 0);
  return Math.max(0, (Math.max(1, level) - 1) * POINTS_PER_LEVEL - spent);
}
