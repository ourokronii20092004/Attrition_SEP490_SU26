"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Crosshair } from "lucide-react";
import { enemiesApi } from "@/lib/api/enemies";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { EnemyResponse } from "@/lib/types";

const TIER_COLOR: Record<string, string> = {
  Common: "text-fg-muted bg-surface-3",
  Uncommon: "text-success bg-success/10",
  Rare: "text-info bg-info/10",
  Epic: "text-violet-400 bg-violet-500/10",
  Legendary: "text-warning bg-warning/10",
  Boss: "text-danger bg-danger/10",
};

export default function EnemyDetailPage() {
  const params = useParams<{ id: string }>();
  const [enemy, setEnemy] = useState<EnemyResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!params.id) return;
    let ignore = false;
    setLoading(true);
    enemiesApi
      .get(params.id)
      .then((res) => {
        if (!ignore && res.success) setEnemy(res.data);
      })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [params.id]);

  if (loading) {
    return (
      <PageShell size="md">
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
      <PageShell size="md">
        <EmptyState
          title="Enemy not found"
          description="This creature may have been removed from the bestiary."
          action={<Link href="/bestiary"><Button variant="secondary">Back to Bestiary</Button></Link>}
        />
      </PageShell>
    );
  }

  return (
    <PageShell size="md">
      <Link href="/bestiary" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Bestiary
      </Link>

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">{enemy.name}</h1>
        <span className={`rounded-full px-3 py-1 text-sm font-medium ${TIER_COLOR[enemy.tier] ?? "text-fg-muted bg-surface-3"}`}>
          {enemy.tier}
        </span>
      </div>
      <p className="mt-2 flex items-center gap-2 text-fg-muted">
        {enemy.spawnBiome}
        {enemy.isRanged && (
          <span className="inline-flex items-center gap-1 text-info"><Crosshair size={14} /> Ranged</span>
        )}
      </p>

      <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="HP" value={enemy.hp} />
        <Stat label="AD" value={enemy.ad} />
        <Stat label="AP" value={enemy.ap} />
        <Stat label="DEF" value={enemy.def} />
        <Stat label="RES" value={enemy.res} />
        <Stat label="ATK SPD" value={enemy.attackSpeed} />
        <Stat label="EXP" value={enemy.expReward} />
        <Stat label="Gold" value={enemy.goldReward} />
      </div>

      {enemy.lore && (
        <div className="mt-8">
          <h2 className="font-display text-xl font-semibold text-fg">Lore</h2>
          <p className="mt-2 leading-relaxed text-fg-muted">{enemy.lore}</p>
        </div>
      )}

      {enemy.lootTable.length > 0 && (
        <div className="mt-8">
          <h2 className="font-display text-xl font-semibold text-fg">Loot Table</h2>
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
                {enemy.lootTable.map((loot, i) => (
                  <tr key={i} className="border-b border-border/50 last:border-0 transition-colors hover:bg-surface-2/50">
                    <td className="px-4 py-2.5 font-medium text-fg">{loot.itemName}</td>
                    <td className="px-4 py-2.5 text-fg-muted">{loot.rarity}</td>
                    <td className="px-4 py-2.5 tabular-nums text-fg-muted">{(loot.dropChance * 100).toFixed(1)}%</td>
                    <td className="px-4 py-2.5 tabular-nums text-fg-muted">{loot.minQty}-{loot.maxQty}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        </div>
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
