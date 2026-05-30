/** The three enemy classifications. Mirror of Enemy.Service EnemyTiers. */
export const ENEMY_TIERS = ["Normal", "Elite", "Boss"] as const;

export type EnemyTier = (typeof ENEMY_TIERS)[number];

/** Tailwind classes for each tier badge. */
export const TIER_COLOR: Record<string, string> = {
  Normal: "text-fg-muted bg-surface-3",
  Elite: "text-info bg-info/10",
  Boss: "text-danger bg-danger/10",
};
