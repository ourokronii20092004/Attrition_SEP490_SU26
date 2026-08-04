"use client";

import { useQuery } from "@tanstack/react-query";
import { Shirt, Hand, Footprints, HardHat, Sparkles, Gem, Package } from "lucide-react";
import { itemsApi } from "@/lib/api/items";
import { skillsApi } from "@/lib/api/skills";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";

interface SlotSave { itemId: string; amount: number }
interface InventorySave {
  equipmentSlots?: SlotSave[];
  accessorySlots?: SlotSave[];
  materialSlots?: SlotSave[];
  equippedHead?: SlotSave;
  equippedChest?: SlotSave;
  equippedLegs?: SlotSave;
  equippedBoots?: SlotSave;
  equippedSkill?: SlotSave;
  equippedAccessory?: SlotSave;
}

/** What the catalogue tells us about a saved id. Absent when the id isn't in the catalogue. */
interface CatalogEntry { name: string; image: string | null }

const isFilled = (s?: SlotSave): s is SlotSave => !!s && !!s.itemId && s.itemId.trim() !== "";

/**
 * Bag capacities, mirroring the `[Capacity(n)]` attributes on PlayerInventory's networked arrays.
 * The save blob encodes slot position by list index (every slot is written, empties included), so we
 * render a fixed grid of this size and index into it — that is what makes item placement visible.
 * A shorter list (older save) just leaves the tail empty; a longer one is clamped by the caller.
 */
const BAG_CAPACITY = { equipmentSlots: 40, accessorySlots: 10, materialSlots: 14 } as const;

const EQUIP_SLOTS: { key: keyof InventorySave; label: string; icon: React.ComponentType<{ size?: number; className?: string }> }[] = [
  { key: "equippedHead", label: "Head", icon: HardHat },
  { key: "equippedChest", label: "Chest", icon: Shirt },
  { key: "equippedLegs", label: "Legs", icon: Hand },
  { key: "equippedBoots", label: "Boots", icon: Footprints },
  { key: "equippedSkill", label: "Skill", icon: Sparkles },
  { key: "equippedAccessory", label: "Accessory", icon: Gem },
];

/**
 * Resolves saved slot ids to catalogue name + artwork. The equipped-skill slot holds a skill id,
 * not an item id, so both catalogues are consulted — items win a collision, being the larger set.
 * Both lists are small, cached, and already shared with the /items and /skills pages.
 */
function useCatalog(): Map<string, CatalogEntry> {
  const { data: items = [] } = useQuery({
    queryKey: qk.items.list(),
    queryFn: async () => {
      const res = await itemsApi.list();
      return res.success ? res.data ?? [] : [];
    },
    staleTime: 5 * 60_000,
  });
  const { data: skills = [] } = useQuery({
    queryKey: qk.skills.list(),
    queryFn: async () => {
      const res = await skillsApi.list();
      return res.success ? res.data ?? [] : [];
    },
    staleTime: 5 * 60_000,
  });

  const map = new Map<string, CatalogEntry>();
  for (const s of skills) map.set(s.skillId, { name: s.name, image: resolveMediaUrl(s.imageUrl) });
  for (const i of items) map.set(i.itemId, { name: i.name, image: resolveMediaUrl(i.imageUrl) });
  return map;
}

/**
 * Renders a character's saved inventory blob (the JSON the Unity client pushes). The shape is
 * { equipped* slots, equipmentSlots[], accessorySlots[], materialSlots[] } with each slot an
 * { itemId, amount }. Ids are resolved against the item/skill catalogues so a slot shows the
 * artwork and human name; an id the catalogue doesn't know falls back to showing the raw id.
 * Bags render as fixed grids so a slot's position matches where the player actually put the item
 * in-game. Gracefully degrades if the blob is missing or malformed.
 */
export function InventoryView({ json }: { json: string | null | undefined }) {
  const catalog = useCatalog();

  if (!json) {
    return <p className="text-sm text-fg-subtle">No inventory saved for this character.</p>;
  }

  let data: InventorySave;
  try {
    data = JSON.parse(json);
  } catch {
    return <p className="text-sm text-danger">Inventory data is corrupted and could not be read.</p>;
  }

  const equipped = EQUIP_SLOTS.map((s) => ({ ...s, slot: data[s.key] as SlotSave | undefined })).filter((s) => isFilled(s.slot));

  // Keep every slot, empties included, so index == in-game grid position.
  const bags = (
    [
      { label: "Equipment", key: "equipmentSlots" },
      { label: "Accessories", key: "accessorySlots" },
      { label: "Materials", key: "materialSlots" },
    ] as const
  ).map(({ label, key }) => {
    const saved = data[key] ?? [];
    const size = Math.max(BAG_CAPACITY[key], saved.length);
    return {
      label,
      slots: Array.from({ length: size }, (_, i) => (isFilled(saved[i]) ? saved[i] : undefined)),
      count: saved.filter(isFilled).length,
    };
  });

  const totalItems = equipped.length + bags.reduce((n, b) => n + b.count, 0);

  if (totalItems === 0) {
    return <p className="text-sm text-fg-subtle">Inventory is empty.</p>;
  }

  return (
    <div className="space-y-4">
      {equipped.length > 0 && (
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">Equipped</p>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {equipped.map((s) => {
              const entry = catalog.get(s.slot!.itemId);
              return (
                <div key={s.key as string} className="flex items-center gap-2 rounded-lg border border-border bg-surface-2 px-3 py-2">
                  {entry?.image ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={entry.image} alt="" className="size-8 shrink-0 rounded object-cover" />
                  ) : (
                    <s.icon size={15} className="shrink-0 text-accent" aria-hidden />
                  )}
                  <div className="min-w-0">
                    <p className="text-[10px] uppercase tracking-wider text-fg-subtle">{s.label}</p>
                    <p className="truncate text-sm text-fg">{entry?.name ?? s.slot!.itemId}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {bags.map((bag) =>
        bag.count > 0 ? (
          <div key={bag.label}>
            <p className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
              <Package size={12} /> {bag.label} ({bag.count}/{bag.slots.length})
            </p>
            <div className="grid grid-cols-5 gap-1.5 sm:grid-cols-8 lg:grid-cols-10">
              {bag.slots.map((it, i) => {
                if (!it) {
                  return (
                    <div
                      key={i}
                      title={`Slot ${i + 1}: empty`}
                      className="aspect-square rounded-md border border-dashed border-border/50 bg-surface-1/30"
                    />
                  );
                }
                const entry = catalog.get(it.itemId);
                const label = entry?.name ?? it.itemId;
                return (
                  <div
                    key={i}
                    title={`Slot ${i + 1}: ${label}${it.amount > 1 ? ` ×${it.amount}` : ""}`}
                    className="relative flex aspect-square items-center justify-center overflow-hidden rounded-md border border-border bg-surface-2"
                  >
                    {entry?.image ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={entry.image} alt={label} className="size-full object-cover" />
                    ) : (
                      <span className="line-clamp-2 break-all p-1 text-center text-[9px] leading-tight text-fg">
                        {label}
                      </span>
                    )}
                    {it.amount > 1 && (
                      <span className="absolute bottom-0 right-0 rounded-tl bg-surface-3/95 px-1 text-[9px] tabular-nums text-fg-muted">
                        {it.amount}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        ) : null,
      )}
    </div>
  );
}
