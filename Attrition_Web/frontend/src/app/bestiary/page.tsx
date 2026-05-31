"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Search, Skull } from "lucide-react";
import { enemiesApi } from "@/lib/api/enemies";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { FilterPills } from "@/components/ui/filter-pills";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { ENEMY_TIERS, TIER_COLOR } from "@/lib/enemy-tiers";
import { qk } from "@/lib/query-keys";

const TIERS = ENEMY_TIERS;

export default function BestiaryPage() {
  const [tier, setTier] = useState("");
  const [search, setSearch] = useState("");

  const { data: enemies = [], isPending } = useQuery({
    queryKey: qk.enemies.list({ tier, search }),
    queryFn: async () => {
      const res = await enemiesApi.list({ tier: tier || undefined, search: search || undefined });
      return res.success ? res.data ?? [] : [];
    },
  });

  const tierOptions = [
    { value: "", label: "All Tiers" },
    ...TIERS.map((t) => ({ value: t, label: t })),
  ];

  return (
    <PageShell>
      <PageTitle description="Every creature that stalks the Attrition world.">Bestiary</PageTitle>

      <div className="flex flex-col gap-3">
        <div className="relative max-w-sm">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search enemies..."
            className="pl-9"
            aria-label="Search enemies"
          />
        </div>
        <FilterPills options={tierOptions} value={tier} onChange={setTier} />
      </div>

      {isPending ? (
        <SkeletonGrid count={6} className="mt-6 lg:grid-cols-3" />
      ) : !enemies.length ? (
        <EmptyState
          icon={Skull}
          title="No enemies found"
          description="Try a different search or tier filter."
          className="mt-6"
        />
      ) : (
        <div className="stagger mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {enemies.map((enemy, i) => (
            <Card key={enemy.enemyId} interactive style={{ "--i": i } as React.CSSProperties} className="p-0">
              <Link href={`/bestiary/${enemy.enemyId}`} className="group block p-5">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="truncate font-display text-lg font-semibold text-fg transition-colors group-hover:text-accent">
                    {enemy.name}
                  </h3>
                  <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ${TIER_COLOR[enemy.tier] ?? "text-fg-muted bg-surface-3"}`}>
                    {enemy.tier}
                  </span>
                </div>
                <p className="mt-1 text-xs text-fg-muted">{enemy.spawnBiome}</p>
                <div className="mt-4 grid grid-cols-4 gap-2 border-t border-border pt-3 text-center">
                  <Stat label="HP" value={enemy.hp} />
                  <Stat label="AD" value={enemy.ad} />
                  <Stat label="DEF" value={enemy.def} />
                  <Stat label="SPD" value={enemy.attackSpeed} />
                </div>
              </Link>
            </Card>
          ))}
        </div>
      )}
    </PageShell>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <span className="block text-[10px] uppercase tracking-wider text-fg-subtle">{label}</span>
      <span className="mt-0.5 block text-sm font-medium tabular-nums text-fg">{value}</span>
    </div>
  );
}
