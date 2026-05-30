"use client";

import { useEffect, useState } from "react";
import { enemiesApi } from "@/lib/api/enemies";
import { PageLoader } from "@/components/ui/spinner";
import type { EnemyResponse } from "@/lib/types";
import Link from "next/link";

const TIERS = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Boss"];

export default function BestiaryPage() {
  const [enemies, setEnemies] = useState<EnemyResponse[]>([]);
  const [tier, setTier] = useState("");
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    enemiesApi
      .list({ tier: tier || undefined, search: search || undefined })
      .then((res) => {
        if (res.success) setEnemies(res.data);
      })
      .finally(() => setLoading(false));
  }, [tier, search]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Bestiary</h1>
      <p className="mt-2 text-fg-muted">All creatures of the Attrition world</p>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search enemies..."
          className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
        />
        <select
          value={tier}
          onChange={(e) => setTier(e.target.value)}
          className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none"
        >
          <option value="">All Tiers</option>
          {TIERS.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
      </div>

      {loading ? (
        <PageLoader />
      ) : (
        <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {enemies.map((enemy) => (
            <Link key={enemy.enemyId} href={`/bestiary/${enemy.enemyId}`} className="card p-4 transition hover:border-accent">
              <div className="flex items-center justify-between">
                <h3 className="font-medium text-fg">{enemy.name}</h3>
                <span className="rounded bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{enemy.tier}</span>
              </div>
              <p className="mt-2 text-xs text-fg-muted">{enemy.spawnBiome}</p>
              <div className="mt-3 grid grid-cols-4 gap-2 text-center text-xs">
                <div><span className="text-fg-subtle">HP</span><br /><span className="text-fg">{enemy.hp}</span></div>
                <div><span className="text-fg-subtle">AD</span><br /><span className="text-fg">{enemy.ad}</span></div>
                <div><span className="text-fg-subtle">DEF</span><br /><span className="text-fg">{enemy.def}</span></div>
                <div><span className="text-fg-subtle">SPD</span><br /><span className="text-fg">{enemy.attackSpeed}</span></div>
              </div>
            </Link>
          ))}
          {enemies.length === 0 && <p className="col-span-full py-8 text-center text-fg-muted">No enemies found.</p>}
        </div>
      )}
    </div>
  );
}
