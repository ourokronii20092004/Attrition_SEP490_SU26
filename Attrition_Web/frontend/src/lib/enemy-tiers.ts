/** The three enemy classifications. Mirror of Enemy.Service EnemyTiers. */
export const ENEMY_TIERS = ["Normal", "Elite", "Boss"] as const;

export type EnemyTier = (typeof ENEMY_TIERS)[number];

/** Tailwind classes for each tier badge. */
export const TIER_COLOR: Record<string, string> = {
  Normal: "text-fg-muted bg-surface-3",
  Elite: "text-info bg-info/10",
  Boss: "text-danger bg-danger/10",
};

/**
 * Ladder position for sorting (Normal < Elite < Boss). Sorting the raw string would order
 * Boss/Elite/Normal alphabetically, which tells an operator nothing about threat.
 * Unrecognised values sort before Normal rather than vanishing.
 */
export function tierRank(value: string | null | undefined): number {
  if (!value) return -1;
  const needle = value.trim().toLowerCase();
  return ENEMY_TIERS.findIndex((t) => t.toLowerCase() === needle);
}
