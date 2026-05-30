"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { enemiesApi } from "@/lib/api/enemies";
import { PageLoader } from "@/components/ui/spinner";
import type { EnemyResponse } from "@/lib/types";

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

  if (loading) return <PageLoader />;
  if (!enemy) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Enemy Not Found</h1>
        <Link href="/bestiary" className="mt-4 inline-block text-accent hover:underline">Back to Bestiary</Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href="/bestiary" className="text-sm text-accent hover:underline">&larr; Bestiary</Link>

      <div className="mt-4">
        <div className="flex items-center gap-3">
          <h1 className="font-display text-4xl font-bold text-fg">{enemy.name}</h1>
          <span className="rounded bg-surface-3 px-2 py-1 text-sm text-fg-muted">{enemy.tier}</span>
        </div>
        <p className="mt-2 text-fg-muted">Biome: {enemy.spawnBiome} {enemy.isRanged && "• Ranged"}</p>
      </div>

      <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
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
        <div className="mt-6">
          <h2 className="font-display text-xl font-semibold text-fg">Lore</h2>
          <p className="mt-2 text-fg-muted leading-relaxed">{enemy.lore}</p>
        </div>
      )}

      {enemy.lootTable.length > 0 && (
        <div className="mt-6">
          <h2 className="font-display text-xl font-semibold text-fg">Loot Table</h2>
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-fg-muted">
                  <th className="pb-2 pr-4">Item</th>
                  <th className="pb-2 pr-4">Rarity</th>
                  <th className="pb-2 pr-4">Drop %</th>
                  <th className="pb-2">Qty</th>
                </tr>
              </thead>
              <tbody>
                {enemy.lootTable.map((loot, i) => (
                  <tr key={i} className="border-b border-border/50">
                    <td className="py-2 pr-4 text-fg">{loot.itemName}</td>
                    <td className="py-2 pr-4 text-fg-muted">{loot.rarity}</td>
                    <td className="py-2 pr-4 text-fg-muted">{(loot.dropChance * 100).toFixed(1)}%</td>
                    <td className="py-2 text-fg-muted">{loot.minQty}-{loot.maxQty}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="card p-3 text-center">
      <p className="text-xs text-fg-subtle">{label}</p>
      <p className="mt-1 text-lg font-semibold text-fg">{value}</p>
    </div>
  );
}
