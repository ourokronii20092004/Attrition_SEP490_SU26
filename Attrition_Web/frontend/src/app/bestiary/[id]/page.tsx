"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Crosshair } from "lucide-react";
import { enemiesApi } from "@/lib/api/enemies";
import { itemsApi } from "@/lib/api/items";
import { skillsApi } from "@/lib/api/skills";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Reveal } from "@/components/ui/reveal";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { TIER_COLOR } from "@/lib/enemy-tiers";
import { rarityColor } from "@/lib/rarity";
import { resolveLootTarget } from "@/lib/loot-target";
import { qk } from "@/lib/query-keys";

export default function EnemyDetailPage() {
  const params = useParams<{ id: string }>();

  const { data: enemy, isPending } = useQuery({
    queryKey: qk.enemies.detail(params.id),
    enabled: !!params.id,
    queryFn: async () => {
      const res = await enemiesApi.get(params.id);
      return res.success ? res.data : null;
    },
  });

  // Both catalogues, so a loot row can be resolved to whichever it names. Cheap and cached: the
  // items and skills lists are small and shared with their own pages.
  const { data: items = [] } = useQuery({
    queryKey: qk.items.list(),
    queryFn: async () => {
      const res = await itemsApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });
  const { data: skills = [] } = useQuery({
    queryKey: qk.skills.list(),
    queryFn: async () => {
      const res = await skillsApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  if (isPending) {
    return (
      <PageShell>
        <Skeleton className="h-4 w-20" />
        <Skeleton className="mt-4 h-10 w-1/2" />
        <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-card" />)}
        </div>
      </PageShell>
    );
  }

  if (!enemy) {
    return (
      <PageShell>
        <EmptyState
          title="Enemy not found"
          description="This creature may have been removed from the bestiary."
          action={<Link href="/bestiary"><Button variant="secondary">Back to Bestiary</Button></Link>}
        />
      </PageShell>
    );
  }

  return (
    <PageShell>
      <BackButton href="/bestiary" label="Bestiary" />

      <Reveal className="mt-6">
        {enemy.imageUrl && (
          <div className="aspect-[21/9] w-full overflow-hidden rounded-xl border border-border bg-surface-2">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={resolveMediaUrl(enemy.imageUrl) ?? ""} alt={enemy.name} className="h-full w-full object-cover" />
          </div>
        )}

        <p className={`${enemy.imageUrl ? "mt-6" : ""} font-mono text-[11px] uppercase tracking-[0.3em] text-accent`}>
          {enemy.spawnBiome || "Bestiary"}
        </p>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <h1 className="font-display text-4xl font-bold tracking-tight text-fg sm:text-5xl">{enemy.name}</h1>
          <span className={`rounded-full px-3 py-1 text-sm font-medium ${TIER_COLOR[enemy.tier] ?? "text-fg-muted bg-surface-3"}`}>
            {enemy.tier}
          </span>
          {enemy.isRanged && (
            <span className="inline-flex items-center gap-1 text-sm text-info"><Crosshair size={14} /> Ranged</span>
          )}
        </div>
      </Reveal>

      <Reveal delay={1} className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="HP" value={enemy.hp} />
        <Stat label="AD" value={enemy.ad} />
        <Stat label="AP" value={enemy.ap} />
        <Stat label="DEF" value={enemy.def} />
        <Stat label="RES" value={enemy.res} />
        <Stat label="ATK SPD" value={enemy.attackSpeed} />
        <Stat label="EXP" value={enemy.expReward} />
        <Stat label="Gold" value={enemy.goldReward} />
      </Reveal>

      {enemy.lore && (
        <Reveal as="section" className="mt-10">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">Lore</h2>
          <p className="mt-3 leading-relaxed text-fg-muted">{enemy.lore}</p>
        </Reveal>
      )}

      {enemy.lootTable.length > 0 && (
        <Reveal as="section" className="mt-10">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">Loot Table</h2>
          <Card className="mt-3 overflow-hidden p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-surface-2 text-left text-xs uppercase tracking-wider text-fg-muted">
                  <th className="px-4 py-2.5 font-medium">Item</th>
                  <th className="px-4 py-2.5 font-medium">Rarity</th>
                  <th className="px-4 py-2.5 font-medium">Drop %</th>
                  <th className="px-4 py-2.5 font-medium">Qty</th>
                </tr>
              </thead>
              <tbody>
                {enemy.lootTable.map((loot, i) => {
                  // A loot row's name points at either the item catalogue or the skill catalogue:
                  // bosses drop their element's skill. Resolving both makes the row a real link and
                  // shows the catalogue's rarity instead of the loot row's stale copy.
                  const target = resolveLootTarget(loot.itemName, loot.rarity, items, skills);
                  return (
                    <tr key={i} className="border-b border-border/50 last:border-0 transition-colors hover:bg-surface-2/50">
                      <td className="px-4 py-2.5 font-medium text-fg">
                        {target.href ? (
                          <Link href={target.href} className="transition-colors hover:text-accent">{target.name}</Link>
                        ) : (
                          target.name
                        )}
                        {target.kind === "skill" && (
                          <span className="ml-2 rounded-full bg-accent/10 px-2 py-0.5 text-[11px] font-medium text-accent">Skill</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${rarityColor(target.rarity)}`}>{target.rarity}</span>
                      </td>
                      <td className="px-4 py-2.5 tabular-nums text-fg-muted">{(loot.dropChance * 100).toFixed(1)}%</td>
                      <td className="px-4 py-2.5 tabular-nums text-fg-muted">{loot.minQty}-{loot.maxQty}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </Card>
        </Reveal>
      )}
    </PageShell>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <Card className="p-3 text-center">
      <p className="text-[10px] uppercase tracking-wider text-fg-subtle">{label}</p>
      <p className="mt-1 text-lg font-semibold tabular-nums text-fg">{value}</p>
    </Card>
  );
}
