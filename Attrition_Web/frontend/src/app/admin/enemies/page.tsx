"use client";

import { useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { enemiesApi } from "@/lib/api/enemies";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { PageLoader } from "@/components/ui/spinner";
import { ENEMY_TIERS } from "@/lib/enemy-tiers";
import { qk } from "@/lib/query-keys";
import type { EnemyResponse, EnemyCreateRequest, EnemyUpdateRequest } from "@/lib/types";

export default function AdminEnemiesPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<EnemyResponse | null>(null);

  const { data: enemies = [], isPending: loading } = useQuery({
    queryKey: qk.admin.enemies(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await enemiesApi.list();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.enemies() });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => { await enemiesApi.delete(id); },
    onSuccess: invalidate,
  });

  const handleDelete = async (id: string) => {
    if (!(await confirm({ message: "Delete this enemy?", danger: true, confirmLabel: "Delete" }))) return;
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

const EMPTY_LOOT = { itemName: "", rarity: "Common", iconKey: null, dropChance: 0.1, minQty: 1, maxQty: 1 };

const lootSchema = z.object({
  itemName: z.string().min(1, "Required"),
  rarity: z.string().min(1, "Required"),
  iconKey: z.string().nullable(),
  dropChance: z.coerce.number().min(0).max(1),
  minQty: z.coerce.number().int().min(1),
  maxQty: z.coerce.number().int().min(1),
});

const enemySchema = z.object({
  enemyId: z.string().min(1, "Enemy ID is required."),
  name: z.string().min(1, "Name is required."),
  tier: z.string().min(1),
  spawnBiome: z.string(),
  hp: z.coerce.number().int().min(1),
  ad: z.coerce.number().int().min(0),
  ap: z.coerce.number().int().min(0),
  def: z.coerce.number().int().min(0),
  res: z.coerce.number().int().min(0),
  attackSpeed: z.coerce.number().min(0),
  isRanged: z.boolean(),
  expReward: z.coerce.number().int().min(0),
  goldReward: z.coerce.number().int().min(0),
  lore: z.string(),
  lootTable: z.array(lootSchema),
});

type EnemyFormValues = z.infer<typeof enemySchema>;

function EnemyForm({ initial, onDone, onCancel }: { initial: EnemyResponse | null; onDone: () => void; onCancel: () => void }) {
  const [error, setError] = useState<string | null>(null);

  const {
    register, handleSubmit, control,
    formState: { errors, isSubmitting },
  } = useForm<EnemyFormValues>({
    resolver: zodResolver(enemySchema),
    defaultValues: {
      enemyId: initial?.enemyId ?? "",
      name: initial?.name ?? "",
      tier: initial?.tier ?? "Normal",
      spawnBiome: initial?.spawnBiome ?? "",
      hp: initial?.hp ?? 100,
      ad: initial?.ad ?? 10,
      ap: initial?.ap ?? 0,
      def: initial?.def ?? 5,
      res: initial?.res ?? 5,
      attackSpeed: initial?.attackSpeed ?? 1,
      isRanged: initial?.isRanged ?? false,
      expReward: initial?.expReward ?? 10,
      goldReward: initial?.goldReward ?? 5,
      lore: initial?.lore ?? "",
      lootTable: initial?.lootTable ?? [],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "lootTable" });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    const { enemyId, ...rest } = values;
    const base = { ...rest, spawnBiome: rest.spawnBiome || undefined, lore: rest.lore || undefined };
    try {
      if (initial) {
        await enemiesApi.update(initial.enemyId, base as EnemyUpdateRequest);
      } else {
        await enemiesApi.create({ enemyId, ...base } as EnemyCreateRequest);
      }
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to save the enemy. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="mt-4 card p-4 space-y-4">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <div className="grid gap-3 sm:grid-cols-2">
        {!initial && <Input label="Enemy ID" error={errors.enemyId?.message} {...register("enemyId")} />}
        <Input label="Name" error={errors.name?.message} {...register("name")} />
        <Select label="Tier" {...register("tier")}>
          {ENEMY_TIERS.map((t) => <option key={t} value={t}>{t}</option>)}
        </Select>
        <Input label="Biome" {...register("spawnBiome")} />
        <Input label="HP" type="number" error={errors.hp?.message} {...register("hp")} />
        <Input label="AD" type="number" {...register("ad")} />
        <Input label="AP" type="number" {...register("ap")} />
        <Input label="DEF" type="number" {...register("def")} />
        <Input label="RES" type="number" {...register("res")} />
        <Input label="ATK Speed" type="number" step="0.01" {...register("attackSpeed")} />
        <Input label="EXP" type="number" {...register("expReward")} />
        <Input label="Gold" type="number" {...register("goldReward")} />
      </div>
      <label className="flex items-center gap-2 text-sm text-fg-muted">
        <input type="checkbox" {...register("isRanged")} className="rounded border-border" />
        Ranged
      </label>
      <div className="space-y-1">
        <label htmlFor="enemy-lore" className="block text-sm font-medium text-fg-muted">Lore</label>
        <textarea
          id="enemy-lore"
          {...register("lore")}
          rows={3}
          className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent"
        />
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-fg">Loot Table</h3>
          <Button type="button" size="sm" variant="secondary" onClick={() => append({ ...EMPTY_LOOT })}>
            Add Drop
          </Button>
        </div>
        {fields.map((field, i) => (
          <div key={field.id} className="grid grid-cols-6 gap-2 items-end">
            <Input label="Item" error={errors.lootTable?.[i]?.itemName?.message} {...register(`lootTable.${i}.itemName`)} />
            <Input label="Rarity" {...register(`lootTable.${i}.rarity`)} />
            <Input label="Drop %" type="number" step="0.01" {...register(`lootTable.${i}.dropChance`)} />
            <Input label="Min" type="number" {...register(`lootTable.${i}.minQty`)} />
            <Input label="Max" type="number" {...register(`lootTable.${i}.maxQty`)} />
            <Button type="button" size="sm" variant="danger" onClick={() => remove(i)}>X</Button>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <Button type="submit" loading={isSubmitting}>{initial ? "Update" : "Create"}</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
