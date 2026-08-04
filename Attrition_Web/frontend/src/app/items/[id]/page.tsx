"use client";

import { useMemo } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Gem, Package, Sparkles, Swords } from "lucide-react";
import { itemsApi } from "@/lib/api/items";
import { enemiesApi } from "@/lib/api/enemies";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { rarityColor } from "@/lib/rarity";

export default function ItemDetailPage() {
  const params = useParams<{ id: string }>();

  const { data: item, isPending } = useQuery({
    queryKey: qk.items.detail(params.id),
    enabled: !!params.id,
    queryFn: async () => {
      const res = await itemsApi.get(params.id);
      return res.success ? res.data : null;
    },
  });

  // Loot tables reference items by name, so the drop list is resolved from the enemy side.
  const { data: enemies = [] } = useQuery({
    queryKey: qk.enemies.list(),
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data ?? [] : [];
    },
  });

  const drops = useMemo(() => {
    if (!item) return [];
    return enemies
      .flatMap((enemy) =>
        (enemy.lootTable ?? [])
          .filter((loot) => loot.itemName === item.name)
          .map((loot) => ({
            enemyId: enemy.enemyId,
            enemyName: enemy.name,
            dropChance: loot.dropChance,
            minQty: loot.minQty,
            maxQty: loot.maxQty,
          })),
      )
      .sort((a, b) => b.dropChance - a.dropChance);
  }, [enemies, item]);

  if (isPending) {
    return (
      <PageShell size="md">
        <div className="mb-5"><BackButton label="Back to items" fallbackHref="/items" /></div>
        <Skeleton className="aspect-[16/9] w-full rounded-card" />
        <Skeleton className="mt-6 h-9 w-2/3" />
        <Skeleton className="mt-3 h-20 w-full" />
      </PageShell>
    );
  }

  if (!item) {
    return (
      <PageShell size="md">
        <div className="mb-5"><BackButton label="Back to items" fallbackHref="/items" /></div>
        <EmptyState icon={Gem} title="Item not found" description="This item doesn't exist or has been removed." />
      </PageShell>
    );
  }

  const image = item.imageUrl ? resolveMediaUrl(item.imageUrl) : null;

  return (
    <PageShell size="md">
      <div className="mb-5"><BackButton label="Back to items" fallbackHref="/items" /></div>

      {image ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={image} alt="" className="aspect-[16/9] w-full rounded-card border border-border object-cover" />
      ) : (
        <div className="flex aspect-[16/9] w-full items-center justify-center rounded-card border border-border bg-surface-2">
          <Gem size={56} className="text-accent" aria-hidden />
        </div>
      )}

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">{item.name}</h1>
        <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${rarityColor(item.rarity)}`}>
          {item.rarity}
        </span>
        {item.isKeyItem && (
          <span className="rounded-full bg-warning/10 px-2.5 py-1 text-xs font-semibold text-warning">Key item</span>
        )}
      </div>

      <p className="mt-3 text-fg-muted">{item.description || "No description yet."}</p>

      <div className="mt-6 grid gap-3 sm:grid-cols-3">
        <Fact icon={Package} label="Category" value={item.category} />
        <Fact icon={Sparkles} label="Max stack" value={item.isKeyItem ? "—" : String(item.maxStack)} />
        <Fact icon={Swords} label="Drops from" value={drops.length ? `${drops.length} enemy type${drops.length > 1 ? "s" : ""}` : "Not dropped"} />
      </div>

      {item.modifiers.length > 0 && (
        <section className="mt-8">
          <h2 className="font-display text-lg font-semibold text-fg">Stats</h2>
          <Card className="mt-3 divide-y divide-border p-0">
            {item.modifiers.map((mod, i) => (
              <div key={`${mod.stat}-${i}`} className="flex items-center justify-between px-4 py-2.5 text-sm">
                <span className="text-fg-muted">{mod.stat}</span>
                <span className={`font-medium tabular-nums ${mod.amount < 0 ? "text-danger" : "text-fg"}`}>
                  {mod.amount > 0 ? `+${mod.amount}` : mod.amount}
                </span>
              </div>
            ))}
          </Card>
        </section>
      )}

      <section className="mt-8">
        <h2 className="font-display text-lg font-semibold text-fg">Where it drops</h2>
        {drops.length === 0 ? (
          <p className="mt-3 rounded-lg border border-dashed border-border px-4 py-6 text-center text-sm text-fg-muted">
            Nothing is known to drop this item. It may be crafted, given, or found in the world.
          </p>
        ) : (
          <Card className="mt-3 divide-y divide-border p-0">
            {drops.map((drop) => (
              <Link
                key={`${drop.enemyId}-${drop.dropChance}`}
                href={`/bestiary/${encodeURIComponent(drop.enemyId)}`}
                className="flex items-center justify-between gap-3 px-4 py-3 text-sm transition-colors hover:bg-surface-2"
              >
                <span className="truncate font-medium text-fg">{drop.enemyName}</span>
                <span className="shrink-0 text-fg-muted">
                  {drop.minQty === drop.maxQty ? `×${drop.minQty}` : `×${drop.minQty}–${drop.maxQty}`}
                  {" · "}
                  <span className="tabular-nums text-fg">{(drop.dropChance * 100).toFixed(0)}%</span>
                </span>
              </Link>
            ))}
          </Card>
        )}
      </section>
    </PageShell>
  );
}

function Fact({ icon: Icon, label, value }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <Card className="p-4">
      <p className="flex items-center gap-1.5 text-xs uppercase tracking-wider text-fg-subtle">
        <Icon size={13} className="text-accent" aria-hidden /> {label}
      </p>
      <p className="mt-1.5 font-medium text-fg">{value}</p>
    </Card>
  );
}
