"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Map as MapIcon, Skull } from "lucide-react";
import Link from "next/link";
import { enemiesApi } from "@/lib/api/enemies";
import type { EnemyResponse } from "@/lib/types";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { TIER_COLOR } from "@/lib/enemy-tiers";
import { qk } from "@/lib/query-keys";

interface WorldArea {
  biome: string;
  enemies: EnemyResponse[];
  bossCount: number;
}

// World/Map chưa có bảng riêng ở backend — nhưng mỗi quái mang spawnBiome (khu vực nó xuất hiện).
// Trang này gom quái theo biome để tạo "bản đồ thế giới": mỗi area liệt kê cư dân của nó.
function aggregateAreas(enemies: EnemyResponse[]): WorldArea[] {
  const map = new Map<string, WorldArea>();
  for (const e of enemies) {
    const biome = e.spawnBiome?.trim() || "Uncharted";
    const area = map.get(biome) ?? { biome, enemies: [], bossCount: 0 };
    area.enemies.push(e);
    if (e.tier === "Boss") area.bossCount++;
    map.set(biome, area);
  }
  return [...map.values()].sort((a, b) => b.enemies.length - a.enemies.length);
}

export default function WorldPage() {
  const [search, setSearch] = useState("");

  const { data: enemies = [], isPending } = useQuery({
    queryKey: qk.enemies.list(),
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  const areas = useMemo(() => {
    let list = aggregateAreas(enemies);
    if (search) {
      const q = search.toLowerCase();
      list = list.filter((a) => a.biome.toLowerCase().includes(q));
    }
    return list;
  }, [enemies, search]);

  return (
    <PageShell>
      <PageTitle description="The regions of Attrition and the creatures that claim them.">World</PageTitle>

      <div className="flex flex-wrap items-end gap-3">
        <div className="relative min-w-56 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search regions..."
            className="pl-9"
            aria-label="Search regions"
          />
        </div>
      </div>

      {isPending ? (
        <SkeletonGrid count={4} className="mt-6 lg:grid-cols-2" />
      ) : !areas.length ? (
        <EmptyState
          icon={MapIcon}
          title="No regions found"
          description="Regions appear here once enemies are tagged with a spawn biome."
          className="mt-6"
        />
      ) : (
        <div className="stagger mt-6 grid gap-4 lg:grid-cols-2">
          {areas.map((area, i) => (
            <Card key={area.biome} style={{ "--i": i } as React.CSSProperties} className="p-5">
              <div className="flex items-center justify-between gap-2">
                <h3 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
                  <MapIcon size={18} className="text-accent" />
                  {area.biome}
                </h3>
                <span className="shrink-0 text-xs text-fg-muted">
                  {area.enemies.length} {area.enemies.length === 1 ? "creature" : "creatures"}
                  {area.bossCount > 0 && ` · ${area.bossCount} boss`}
                </span>
              </div>
              <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-3">
                {area.enemies.map((e) => (
                  <Link
                    key={e.enemyId}
                    href={`/bestiary/${e.enemyId}`}
                    className={`group inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium transition-colors ${TIER_COLOR[e.tier] ?? "text-fg-muted bg-surface-3"}`}
                  >
                    <Skull size={11} className="opacity-60 transition-opacity group-hover:opacity-100" />
                    {e.name}
                  </Link>
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}
    </PageShell>
  );
}
