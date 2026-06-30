"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Gem } from "lucide-react";
import { enemiesApi } from "@/lib/api/enemies";
import type { EnemyResponse } from "@/lib/types";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";

// Thứ bậc hiếm + màu hiển thị (khớp tone accent của site).
const RARITY_ORDER = ["Common", "Uncommon", "Rare", "Epic", "Legendary"];
const RARITY_COLOR: Record<string, string> = {
  Common: "text-fg-muted bg-surface-3",
  Uncommon: "text-success bg-success/10",
  Rare: "text-info bg-info/10",
  Epic: "text-[#a274ff] bg-[#a274ff]/10",
  Legendary: "text-warning bg-warning/10",
};

interface AggregatedItem {
  itemName: string;
  rarity: string;
  iconKey: string | null;
  bestDropChance: number;
  sources: { enemyId: string; enemyName: string; dropChance: number }[];
}

// Item không có bảng riêng ở backend — chúng sống trong loot table của từng quái. Trang này tổng
// hợp toàn bộ item xuất hiện trong loot của mọi quái, gộp theo tên + ghi lại "rơi từ quái nào".
function aggregateItems(enemies: EnemyResponse[]): AggregatedItem[] {
  const map = new Map<string, AggregatedItem>();
  for (const e of enemies) {
    for (const loot of e.lootTable ?? []) {
      if (!loot.itemName) continue;
      const existing = map.get(loot.itemName);
      const source = { enemyId: e.enemyId, enemyName: e.name, dropChance: loot.dropChance };
      if (existing) {
        existing.sources.push(source);
        existing.bestDropChance = Math.max(existing.bestDropChance, loot.dropChance);
      } else {
        map.set(loot.itemName, {
          itemName: loot.itemName,
          rarity: loot.rarity || "Common",
          iconKey: loot.iconKey,
          bestDropChance: loot.dropChance,
          sources: [source],
        });
      }
    }
  }
  return [...map.values()];
}

export default function ItemsPage() {
  const [rarity, setRarity] = useState("");
  const [search, setSearch] = useState("");

  const { data: enemies = [], isPending } = useQuery({
    queryKey: qk.enemies.list(),
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  const items = useMemo(() => {
    let list = aggregateItems(enemies);
    if (rarity) list = list.filter((i) => i.rarity === rarity);
    if (search) {
      const q = search.toLowerCase();
      list = list.filter((i) => i.itemName.toLowerCase().includes(q));
    }
    return list.sort((a, b) => {
      const ra = RARITY_ORDER.indexOf(a.rarity);
      const rb = RARITY_ORDER.indexOf(b.rarity);
      if (ra !== rb) return rb - ra; // hiếm trước
      return a.itemName.localeCompare(b.itemName);
    });
  }, [enemies, rarity, search]);

  const rarityOptions = [
    { value: "", label: "All Rarities" },
    ...RARITY_ORDER.map((r) => ({ value: r, label: r })),
  ];

  return (
    <PageShell>
      <PageTitle description="Every item that drops across the Attrition world, and what hunts you for it.">
        Items
      </PageTitle>

      <div className="flex flex-wrap items-end gap-3">
        <div className="relative min-w-56 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search items..."
            className="pl-9"
            aria-label="Search items"
          />
        </div>
        <div className="w-48">
          <Select value={rarity} onChange={(e) => setRarity(e.target.value)} aria-label="Filter by rarity">
            {rarityOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </Select>
        </div>
      </div>

      {isPending ? (
        <SkeletonGrid count={6} className="mt-6 lg:grid-cols-3" />
      ) : !items.length ? (
        <EmptyState
          icon={Gem}
          title="No items found"
          description="Items appear here once enemies are configured to drop them."
          className="mt-6"
        />
      ) : (
        <div className="stagger mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((item, i) => (
            <Card key={item.itemName} style={{ "--i": i } as React.CSSProperties} className="p-5">
              <div className="flex items-center justify-between gap-2">
                <h3 className="truncate font-display text-lg font-semibold text-fg">{item.itemName}</h3>
                <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ${RARITY_COLOR[item.rarity] ?? "text-fg-muted bg-surface-3"}`}>
                  {item.rarity}
                </span>
              </div>
              <p className="mt-1 text-xs text-fg-muted">
                Best drop {(item.bestDropChance * 100).toFixed(0)}% · {item.sources.length} source{item.sources.length > 1 ? "s" : ""}
              </p>
              <div className="mt-4 space-y-1.5 border-t border-border pt-3">
                {item.sources.slice(0, 4).map((s) => (
                  <div key={s.enemyId} className="flex items-center justify-between text-sm">
                    <span className="truncate text-fg-muted">{s.enemyName}</span>
                    <span className="shrink-0 tabular-nums text-fg">{(s.dropChance * 100).toFixed(0)}%</span>
                  </div>
                ))}
                {item.sources.length > 4 && (
                  <p className="text-xs text-fg-subtle">+{item.sources.length - 4} more</p>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}
    </PageShell>
  );
}
