import type { SkillResponse } from "@/lib/types";

/**
 * Rarity, weakest to strongest. Doubles as tier depth in the skill tree: a branch's Common
 * skills sit nearest the root, its Legendary skills furthest out. Matches the item rarity
 * ladder used on the items page.
 */
export const RARITY_TIERS = ["Common", "Uncommon", "Rare", "Epic", "Legendary"] as const;

/** Elements, in the order the game presents them. Each becomes one branch of the tree. */
export const ELEMENTS = ["Fire", "Wood", "Earth", "Thunder", "Thrust"] as const;

/** Per-tier accent, reused for the node ring and the tier label. */
export const TIER_COLOR: Record<string, string> = {
  Common: "text-fg-muted bg-surface-3",
  Uncommon: "text-success bg-success/10",
  Rare: "text-info bg-info/10",
  Epic: "text-accent bg-accent/10",
  Legendary: "text-warning bg-warning/10",
};

export type SkillTier = { rarity: string; skills: SkillResponse[] };
export type SkillBranch = { element: string; tiers: SkillTier[]; total: number };

/** Rarity ladder position; unknown rarities sort last but still render. */
function tierIndex(rarity: string): number {
  const i = RARITY_TIERS.findIndex((r) => r.toLowerCase() === rarity?.toLowerCase());
  return i === -1 ? RARITY_TIERS.length : i;
}

/**
 * Group skills into one branch per element, each branch split into rarity tiers.
 *
 * NOTE ON SHAPE: the skill table has no prerequisite/parent column, so there is no real
 * unlock graph to draw — a tree with specific edges would be invented data. This derives the
 * layout from two fields that do exist (element, rarity): element chooses the branch, rarity
 * sets the depth along it. If per-skill prerequisites are added later, this is the seam to
 * replace — the renderer already draws whatever tiers it is handed.
 *
 * Branches and tiers with no skills are dropped, so filtering never leaves empty scaffolding.
 * Unrecognised elements are kept as their own branch rather than silently hidden.
 */
export function buildSkillTree(skills: SkillResponse[]): SkillBranch[] {
  const byElement = new Map<string, SkillResponse[]>();
  for (const skill of skills) {
    const key = skill.element || "Unaligned";
    const bucket = byElement.get(key);
    if (bucket) bucket.push(skill);
    else byElement.set(key, [skill]);
  }

  const order = (element: string) => {
    const i = ELEMENTS.findIndex((e) => e === element);
    return i === -1 ? ELEMENTS.length : i;
  };

  return [...byElement.entries()]
    .sort(([a], [b]) => order(a) - order(b) || a.localeCompare(b))
    .map(([element, group]) => {
      const tiers: SkillTier[] = [];
      for (const rarity of RARITY_TIERS) {
        const inTier = group.filter((s) => s.rarity?.toLowerCase() === rarity.toLowerCase());
        if (inTier.length) tiers.push({ rarity, skills: sortWithinTier(inTier) });
      }
      // Anything whose rarity isn't on the ladder still deserves a row.
      const unknown = group.filter((s) => tierIndex(s.rarity) === RARITY_TIERS.length);
      if (unknown.length) tiers.push({ rarity: "Other", skills: sortWithinTier(unknown) });

      return { element, tiers, total: group.length };
    });
}

/** Cheapest first inside a tier, so a row reads left-to-right as increasing cost. */
function sortWithinTier(skills: SkillResponse[]): SkillResponse[] {
  return [...skills].sort((a, b) => a.manaCost - b.manaCost || a.name.localeCompare(b.name));
}
