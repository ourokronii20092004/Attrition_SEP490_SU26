/**
 * Item rarity: the canonical ladder, display colours, and tolerant matching.
 *
 * Rarity is stored as free text (`enemy.items."Rarity"`, validated only as non-empty ≤50 chars),
 * so what's in the database is whatever an admin typed. The filter used to compare with `===`
 * against this exact list, which meant "rare" or "Rare " silently matched nothing. Everything
 * here normalises before comparing, and unknown values are surfaced rather than dropped.
 */

/** Weakest to strongest. Also the sort order on the items page. */
export const RARITY_ORDER = ["Common", "Uncommon", "Rare", "Epic", "Legendary"] as const;

export type Rarity = (typeof RARITY_ORDER)[number];

/** Badge classes per rarity. Falls back to the neutral style for anything off-ladder. */
export const RARITY_COLOR: Record<string, string> = {
  Common: "text-fg-muted bg-surface-3",
  Uncommon: "text-success bg-success/10",
  Rare: "text-info bg-info/10",
  Epic: "text-[#a274ff] bg-[#a274ff]/10",
  Legendary: "text-warning bg-warning/10",
};

export const RARITY_FALLBACK_COLOR = "text-fg-muted bg-surface-3";

/** Classes for a rarity badge, tolerant of casing and stray whitespace. */
export function rarityColor(value: string | null | undefined): string {
  const canonical = canonicalRarity(value);
  return (canonical && RARITY_COLOR[canonical]) || RARITY_FALLBACK_COLOR;
}

/**
 * Map a stored value onto the ladder, ignoring case and surrounding whitespace.
 * Returns null when it isn't a recognised rarity, so callers can decide what to do with it.
 */
export function canonicalRarity(value: string | null | undefined): Rarity | null {
  if (!value) return null;
  const needle = value.trim().toLowerCase();
  return RARITY_ORDER.find((r) => r.toLowerCase() === needle) ?? null;
}

/** True when two rarity values mean the same thing despite casing/whitespace differences. */
export function rarityMatches(stored: string | null | undefined, selected: string): boolean {
  if (!selected) return true; // "All rarities"
  const a = canonicalRarity(stored);
  const b = canonicalRarity(selected);
  // Both recognised: compare canonically. Otherwise fall back to a trimmed, case-insensitive
  // comparison so an off-ladder value like "Mythic" can still be filtered on.
  if (a && b) return a === b;
  return (stored ?? "").trim().toLowerCase() === selected.trim().toLowerCase();
}

/** Ladder position for sorting; unrecognised values sort before Common rather than vanishing. */
export function rarityRank(value: string | null | undefined): number {
  const canonical = canonicalRarity(value);
  return canonical ? RARITY_ORDER.indexOf(canonical) : -1;
}
