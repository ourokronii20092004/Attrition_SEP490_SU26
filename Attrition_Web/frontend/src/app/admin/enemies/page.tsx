"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/providers";
import { enemiesApi } from "@/lib/api/enemies";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { PageLoader } from "@/components/ui/spinner";
import { ENEMY_TIERS } from "@/lib/enemy-tiers";
import type { EnemyResponse, EnemyCreateRequest, EnemyUpdateRequest, LootEntryDto } from "@/lib/types";

export default function AdminEnemiesPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<EnemyResponse | null>(null);

  const { data: enemies = [], isPending: loading } = useQuery({
    queryKey: ["admin", "enemies"],
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "enemies"] });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => { await enemiesApi.delete(id); },
    onSuccess: invalidate,
  });

  const handleDelete = (id: string) => {
    if (!confirm("Delete this enemy?")) return;
    deleteMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

  return (
    <div className="mx-auto max-w-6xl">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-3xl font-bold text-fg">Enemies Management</h1>
        <Button onClick={() => { setEditing(null); setShowForm(true); }}>Add Enemy</Button>
      </div>

      {showForm && (
        <EnemyForm
          initial={editing}
          onDone={() => { setShowForm(false); invalidate(); }}
          onCancel={() => setShowForm(false)}
        />
      )}

      <div className="mt-6 space-y-2">
        {enemies.map((e) => (
          <div key={e.enemyId} className="card flex items-center justify-between p-4">
            <div>
              <p className="font-medium text-fg">{e.name}</p>
              <p className="text-xs text-fg-muted">{e.tier} &middot; {e.spawnBiome} &middot; {e.lootTable.length} drops</p>
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="secondary" onClick={() => { setEditing(e); setShowForm(true); }}>Edit</Button>
              <Button size="sm" variant="danger" onClick={() => handleDelete(e.enemyId)}>Delete</Button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

const EMPTY_LOOT: LootEntryDto = { itemName: "", rarity: "Common", iconKey: null, dropChance: 0.1, minQty: 1, maxQty: 1 };

function EnemyForm({ initial, onDone, onCancel }: { initial: EnemyResponse | null; onDone: () => void; onCancel: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const [enemyId, setEnemyId] = useState(initial?.enemyId ?? "");
  const [name, setName] = useState(initial?.name ?? "");
  const [tier, setTier] = useState(initial?.tier ?? "Normal");
  const [spawnBiome, setSpawnBiome] = useState(initial?.spawnBiome ?? "");
  const [hp, setHp] = useState(initial?.hp ?? 100);
  const [ad, setAd] = useState(initial?.ad ?? 10);
  const [ap, setAp] = useState(initial?.ap ?? 0);
  const [def, setDef] = useState(initial?.def ?? 5);
  const [res, setRes] = useState(initial?.res ?? 5);
  const [attackSpeed, setAttackSpeed] = useState(initial?.attackSpeed ?? 1);
  const [isRanged, setIsRanged] = useState(initial?.isRanged ?? false);
  const [expReward, setExpReward] = useState(initial?.expReward ?? 10);
  const [goldReward, setGoldReward] = useState(initial?.goldReward ?? 5);
  const [lore, setLore] = useState(initial?.lore ?? "");
  const [lootTable, setLootTable] = useState<LootEntryDto[]>(initial?.lootTable ?? []);

  const updateLoot = (index: number, field: keyof LootEntryDto, value: any) => {
    setLootTable((prev) => prev.map((entry, i) => i === index ? { ...entry, [field]: value } : entry));
  };

  const removeLoot = (index: number) => {
    setLootTable((prev) => prev.filter((_, i) => i !== index));
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      const base = { name, tier, spawnBiome: spawnBiome || undefined, hp, ad, ap, def, res, attackSpeed, isRanged, expReward, goldReward, lore: lore || undefined, lootTable };
      if (initial) {
        await enemiesApi.update(initial.enemyId, base as EnemyUpdateRequest);
      } else {
        await enemiesApi.create({ enemyId, ...base } as EnemyCreateRequest);
      }
    },
    onSuccess: () => {
      onDone();
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : "Failed to save the enemy. Please try again.");
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    saveMutation.mutate();
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 card p-4 space-y-4">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <div className="grid gap-3 sm:grid-cols-2">
        {!initial && <Input label="Enemy ID" value={enemyId} onChange={(e) => setEnemyId(e.target.value)} />}
        <Input label="Name" value={name} onChange={(e) => setName(e.target.value)} />
        <Select label="Tier" value={tier} onChange={(e) => setTier(e.target.value)}>
          {ENEMY_TIERS.map((t) => <option key={t} value={t}>{t}</option>)}
        </Select>
        <Input label="Biome" value={spawnBiome} onChange={(e) => setSpawnBiome(e.target.value)} />
        <Input label="HP" type="number" value={hp} onChange={(e) => setHp(+e.target.value)} />
        <Input label="AD" type="number" value={ad} onChange={(e) => setAd(+e.target.value)} />
        <Input label="AP" type="number" value={ap} onChange={(e) => setAp(+e.target.value)} />
        <Input label="DEF" type="number" value={def} onChange={(e) => setDef(+e.target.value)} />
        <Input label="RES" type="number" value={res} onChange={(e) => setRes(+e.target.value)} />
        <Input label="ATK Speed" type="number" value={attackSpeed} onChange={(e) => setAttackSpeed(+e.target.value)} />
        <Input label="EXP" type="number" value={expReward} onChange={(e) => setExpReward(+e.target.value)} />
        <Input label="Gold" type="number" value={goldReward} onChange={(e) => setGoldReward(+e.target.value)} />
      </div>
      <label className="flex items-center gap-2 text-sm text-fg-muted">
        <input type="checkbox" checked={isRanged} onChange={(e) => setIsRanged(e.target.checked)} className="rounded border-border" />
        Ranged
      </label>
      <div className="space-y-1">
        <label className="block text-sm font-medium text-fg-muted">Lore</label>
        <textarea
          value={lore}
          onChange={(e) => setLore(e.target.value)}
          rows={3}
          className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent"
        />
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-fg">Loot Table</h3>
          <Button type="button" size="sm" variant="secondary" onClick={() => setLootTable((prev) => [...prev, { ...EMPTY_LOOT }])}>
            Add Drop
          </Button>
        </div>
        {lootTable.map((loot, i) => (
          <div key={i} className="grid grid-cols-6 gap-2 items-end">
            <Input label="Item" value={loot.itemName} onChange={(e) => updateLoot(i, "itemName", e.target.value)} />
            <Input label="Rarity" value={loot.rarity} onChange={(e) => updateLoot(i, "rarity", e.target.value)} />
            <Input label="Drop %" type="number" step="0.01" value={loot.dropChance} onChange={(e) => updateLoot(i, "dropChance", +e.target.value)} />
            <Input label="Min" type="number" value={loot.minQty} onChange={(e) => updateLoot(i, "minQty", +e.target.value)} />
            <Input label="Max" type="number" value={loot.maxQty} onChange={(e) => updateLoot(i, "maxQty", +e.target.value)} />
            <Button type="button" size="sm" variant="danger" onClick={() => removeLoot(i)}>X</Button>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <Button type="submit" loading={saveMutation.isPending}>{initial ? "Update" : "Create"}</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
