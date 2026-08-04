import type { ItemResponse, SkillResponse } from "@/lib/types";

/**
 * What a loot entry actually refers to.
 *
 * Loot tables store a bare `itemName` string, and it points at one of two catalogues: the item
 * table, or the skill table. Bosses drop their element's skill (`BossController.dropsSkillId` in
 * the game; the final boss leaves it empty), so a skill id in loot is correct data, not a stray
 * reference — it just can't be resolved by looking only at items, which is why those five entries
 * appeared to be orphans.
 */
export type LootTarget =
  | { kind: "item"; id: string; name: string; rarity: string; href: string }
  | { kind: "skill"; id: string; name: string; rarity: string; href: string }
  | { kind: "unknown"; id: null; name: string; rarity: string; href: null };

/**
 * Resolve a loot entry's `itemName` against both catalogues.
 *
 * Items are matched on display name (that is what loot rows store), skills on their id — the
 * loot rows carry `skill_fire` and friends, which are Skill.Service ids rather than display names.
 *
 * `storedRarity` is the loot row's own copy, used only as a last resort: it is denormalised and
 * has drifted from both catalogues, so a resolved target's rarity always wins.
 */
export function resolveLootTarget(
  itemName: string,
  storedRarity: string,
  items: ItemResponse[],
  skills: SkillResponse[],
): LootTarget {
  const item = items.find((i) => i.name === itemName);
  if (item) {
    return {
      kind: "item",
      id: item.itemId,
      name: item.name,
      rarity: item.rarity,
      href: `/items/${encodeURIComponent(item.itemId)}`,
    };
  }

  const skill = skills.find((s) => s.skillId === itemName);
  if (skill) {
    return {
      kind: "skill",
      id: skill.skillId,
      name: skill.name || skill.skillId,
      rarity: skill.rarity,
      href: `/skills/${encodeURIComponent(skill.skillId)}`,
    };
  }

  // Neither catalogue knows it: show what the loot row says rather than hiding the drop.
  return { kind: "unknown", id: null, name: itemName, rarity: storedRarity, href: null };
}
