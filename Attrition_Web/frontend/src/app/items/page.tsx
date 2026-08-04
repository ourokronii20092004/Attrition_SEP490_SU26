"use client";

import { Suspense, useMemo } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Gem, Search } from "lucide-react";
import { itemsApi } from "@/lib/api/items";
import { enemiesApi } from "@/lib/api/enemies";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { Pagination } from "@/components/ui/pagination";
import { useUrlPagination, useQueryParam } from "@/lib/hooks/use-url-pagination";
import { RARITY_ORDER, rarityColor, rarityMatches, rarityRank } from "@/lib/rarity";
import type { EnemyResponse } from "@/lib/types";

/** Which enemies drop a given item, keyed by item name (loot tables reference items by name). */
function dropSourcesByItemName(enemies: EnemyResponse[]) {
  const map = new Map<string, { enemyId: string; enemyName: string; dropChance: number }[]>();
  for (const enemy of enemies) {
    for (const loot of enemy.lootTable ?? []) {
      if (!loot.itemName) continue;
      const list = map.get(loot.itemName) ?? [];
      list.push({ enemyId: enemy.enemyId, enemyName: enemy.name, dropChance: loot.dropChance });
      map.set(loot.itemName, list);
    }
  }
  return map;
}

function ItemsList() {
  // Filters + page live in the URL, so returning from an item page restores the same view.
  const [rarity, setRarity] = useQueryParam("rarity");
  const [search, setSearch] = useQueryParam("q");

  // The item catalogue is the source of truth for name/rarity/art. This page used to derive items
  // from enemy loot tables instead, which meant it showed the loot rows' own denormalised rarity —
  // stale and almost entirely "Common" — so filtering by Rare or Epic returned nothing.
  const { data: catalogue = [], isPending } = useQuery({
    queryKey: qk.items.list(),
    queryFn: async () => {
      const res = await itemsApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  // Drop sources are still worth showing, and only the loot tables know them.
  const { data: enemies = [] } = useQuery({
    queryKey: qk.enemies.list(),
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  const sources = useMemo(() => dropSourcesByItemName(enemies), [enemies]);

  const items = useMemo(() => {
    let list = catalogue;
    if (rarity) list = list.filter((i) => rarityMatches(i.rarity, rarity));
    if (search) {
      const q = search.toLowerCase();
      list = list.filter(
        (i) => i.name.toLowerCase().includes(q) || (i.description ?? "").toLowerCase().includes(q),
      );
    }
    return [...list].sort((a, b) => {
      const ra = rarityRank(a.rarity);
      const rb = rarityRank(b.rarity);
      if (ra !== rb) return rb - ra; // rarest first
      return a.name.localeCompare(b.name);
    });
  }, [catalogue, rarity, search]);

  const { page, setPage, totalPages, paged } = useUrlPagination(items, 12);

  return (
    <PageShell>
      <PageTitle description="Every item in the Attrition world — what it does, and what you have to kill for it.">
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
            <option value="">All Rarities</option>
            {RARITY_ORDER.map((r) => (
              <option key={r} value={r}>{r}</option>
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
          description={
            catalogue.length
              ? "No items match that search or rarity."
              : "Items appear here once they're configured in the admin panel."
          }
          className="mt-6"
        />
      ) : (
        <div className="stagger mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {paged.map((item, i) => {
            const drops = sources.get(item.name) ?? [];
            const image = item.imageUrl ? resolveMediaUrl(item.imageUrl) : null;
            return (
              <Link
                key={item.itemId}
                href={`/items/${encodeURIComponent(item.itemId)}`}
                className="group rounded-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                <Card
                  style={{ "--i": i } as React.CSSProperties}
                  className="h-full overflow-hidden transition-transform group-hover:-translate-y-0.5"
                >
                  {image ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={image} alt="" className="aspect-[16/9] w-full object-cover" />
                  ) : (
                    <div className="flex aspect-[16/9] items-center justify-center bg-surface-2">
                      <Gem size={38} className="text-accent" aria-hidden />
                    </div>
                  )}
                  <div className="p-5">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="truncate font-display text-lg font-semibold text-fg group-hover:text-accent">
                        {item.name}
                      </h3>
                      <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ${rarityColor(item.rarity)}`}>
                        {item.rarity}
                      </span>
                    </div>
                    <p className="mt-2 line-clamp-2 min-h-10 text-sm text-fg-muted">
                      {item.description || "No description yet."}
                    </p>
                    <p className="mt-4 border-t border-border pt-3 text-xs text-fg-subtle">
                      {item.category}
                      {drops.length > 0 && ` · drops from ${drops.length} enem${drops.length > 1 ? "ies" : "y"}`}
                      {item.isKeyItem && " · key item"}
                    </p>
                  </div>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
      {!isPending && items.length > 0 && (
        <Pagination page={page} totalPages={totalPages} onChange={setPage} />
      )}
    </PageShell>
  );
}

export default function ItemsPage() {
  return (
    <Suspense fallback={null}>
      <ItemsList />
    </Suspense>
  );
}
