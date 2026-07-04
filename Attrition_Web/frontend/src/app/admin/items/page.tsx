"use client";

import { useState, useEffect } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Gem } from "lucide-react";
import { useAuth, useConfirm } from "@/lib/providers";
import { itemsApi } from "@/lib/api/items";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { AssetImageField } from "@/components/admin/asset-image-field";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { useClientPagination } from "@/lib/hooks/use-client-pagination";
import { qk } from "@/lib/query-keys";
import type { ItemResponse, ItemCreateRequest, ItemUpdateRequest } from "@/lib/types";

const CATEGORIES = ["Equipment", "Accessory", "Skill", "Material"];
const STAT_TYPES = ["MaxHP", "MaxMana", "MaxStamina", "AD", "AP", "DEF", "RES", "MoveSpeed", "AttackSpeed"];

export default function AdminItemsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<ItemResponse | null>(null);
  const [formDirty, setFormDirty] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("all");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: items = [], isPending: loading } = useQuery({
    queryKey: qk.admin.items(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await itemsApi.list();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.items() });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => { await itemsApi.delete(id); },
    onSuccess: invalidate,
  });

  const handleDelete = async (id: string) => {
    if (!(await confirm({ message: "Delete this item?", danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
  };

  const filtered = items.filter((i) => {
    if (categoryFilter !== "all" && i.category !== categoryFilter) return false;
    if (search && !i.name.toLowerCase().includes(search) && !i.itemId.toLowerCase().includes(search)) return false;
    return true;
  });
  const { page, setPage, totalPages, paged } = useClientPagination(filtered, 20);

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

  return (
    <div>
      <AdminPageHeader title="Items" addLabel="Add Item" onAdd={() => { setEditing(null); setShowForm(true); }} />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search items or id…"
        filters={[
          {
            value: categoryFilter, onChange: setCategoryFilter, ariaLabel: "Filter by category",
            options: [{ value: "all", label: "All categories" }, ...CATEGORIES.map((c) => ({ value: c, label: c }))],
          },
        ]}
      />

      <Modal open={showForm} onClose={() => setShowForm(false)} title={editing ? "Edit Item" : "Add Item"} size="lg" dirty={formDirty}>
        <ItemForm
          initial={editing}
          onDirtyChange={setFormDirty}
          onDone={() => { setFormDirty(false); setShowForm(false); invalidate(); }}
          onCancel={() => { setFormDirty(false); setShowForm(false); }}
        />
      </Modal>

      <AdminTable
        columns={[
          { key: "img", label: "" },
          { key: "name", label: "Name" },
          { key: "category", label: "Category" },
          { key: "rarity", label: "Rarity" },
          { key: "mods", label: "Modifiers" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        empty={filtered.length === 0}
      >
        {paged.map((i) => (
          <AdminRow key={i.itemId} onClick={() => { setEditing(i); setShowForm(true); }}>
            <td className="px-3 py-2">
              {i.imageUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={resolveMediaUrl(i.imageUrl) ?? ""} alt="" className="h-9 w-9 rounded object-cover" />
              ) : (
                <div className="flex h-9 w-9 items-center justify-center rounded bg-surface-2 text-fg-subtle"><Gem size={15} /></div>
              )}
            </td>
            <td className="px-3 py-2 font-medium text-fg">{i.name}</td>
            <td className="px-3 py-2 text-fg-muted">{i.category}</td>
            <td className="px-3 py-2 text-fg-muted">{i.rarity}</td>
            <td className="px-3 py-2 tabular-nums text-fg-muted">{i.modifiers.length}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="secondary" onClick={(ev) => { ev.stopPropagation(); setEditing(i); setShowForm(true); }}>Edit</Button>
                <Button size="sm" variant="danger" onClick={(ev) => { ev.stopPropagation(); handleDelete(i.itemId); }}>Delete</Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
    </div>
  );
}

const EMPTY_MOD = { stat: "AD", amount: 1 };

const modifierSchema = z.object({
  stat: z.string().min(1, "Required"),
  amount: z.coerce.number().int(),
});

const itemSchema = z.object({
  itemId: z.string().min(1, "Item ID is required."),
  name: z.string().min(1, "Name is required."),
  category: z.string().min(1),
  rarity: z.string().min(1),
  iconKey: z.string().nullable(),
  description: z.string(),
  imageUrl: z.string().nullable(),
  maxStack: z.coerce.number().int().min(1),
  isKeyItem: z.boolean(),
  modifiers: z.array(modifierSchema),
});

type ItemFormValues = z.infer<typeof itemSchema>;

function ItemForm({ initial, onDone, onCancel, onDirtyChange }: { initial: ItemResponse | null; onDone: () => void; onCancel: () => void; onDirtyChange?: (dirty: boolean) => void }) {
  const [error, setError] = useState<string | null>(null);

  const {
    register, handleSubmit, control,
    formState: { errors, isSubmitting, isDirty },
    watch, setValue,
  } = useForm<ItemFormValues>({
    resolver: zodResolver(itemSchema),
    defaultValues: {
      itemId: initial?.itemId ?? "",
      name: initial?.name ?? "",
      category: initial?.category ?? "Material",
      rarity: initial?.rarity ?? "Common",
      iconKey: initial?.iconKey ?? null,
      description: initial?.description ?? "",
      imageUrl: initial?.imageUrl ?? null,
      maxStack: initial?.maxStack ?? 1,
      isKeyItem: initial?.isKeyItem ?? false,
      modifiers: initial?.modifiers ?? [],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "modifiers" });

  useEffect(() => { onDirtyChange?.(isDirty); }, [isDirty, onDirtyChange]);

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    const { itemId, ...rest } = values;
    const base = { ...rest, iconKey: rest.iconKey || undefined, description: rest.description || undefined };
    try {
      if (initial) {
        await itemsApi.update(initial.itemId, base as ItemUpdateRequest);
      } else {
        await itemsApi.create({ itemId, ...base } as ItemCreateRequest);
      }
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to save the item. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <AssetImageField
        value={watch("imageUrl")}
        onChange={(url) => setValue("imageUrl", url, { shouldDirty: true })}
        sourceType="item"
        sourceId={initial?.itemId}
        label="Item image"
      />
      <div className="grid gap-3 sm:grid-cols-2">
        {!initial && <Input label="Item ID" error={errors.itemId?.message} {...register("itemId")} />}
        <Input label="Name" error={errors.name?.message} {...register("name")} />
        <Select label="Category" {...register("category")}>
          {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </Select>
        <Input label="Rarity" {...register("rarity")} />
        <Input label="Icon Key" {...register("iconKey")} />
        <Input label="Max Stack" type="number" error={errors.maxStack?.message} {...register("maxStack")} />
      </div>
      <label className="flex items-center gap-2 text-sm text-fg-muted">
        <input type="checkbox" {...register("isKeyItem")} className="rounded border-border" />
        Key Item (không thể drop/bán/hủy)
      </label>
      <div className="space-y-1">
        <label htmlFor="item-desc" className="block text-sm font-medium text-fg-muted">Description</label>
        <textarea
          id="item-desc"
          {...register("description")}
          rows={3}
          className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent"
        />
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-fg">Stat Modifiers</h3>
          <Button type="button" size="sm" variant="secondary" onClick={() => append({ ...EMPTY_MOD })}>
            Add Modifier
          </Button>
        </div>
        {fields.map((field, i) => (
          <div key={field.id} className="grid grid-cols-[1fr_1fr_auto] gap-2 items-end">
            <Select label="Stat" {...register(`modifiers.${i}.stat`)}>
              {STAT_TYPES.map((s) => <option key={s} value={s}>{s}</option>)}
            </Select>
            <Input label="Amount" type="number" error={errors.modifiers?.[i]?.amount?.message} {...register(`modifiers.${i}.amount`)} />
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

