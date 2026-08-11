"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { wikiApi } from "@/lib/api/wiki";
import type { WikiCategoryDto } from "@/lib/types";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import {
  AdminPageHeader, AdminFilterBar, AdminTable, AdminRow, applySort, type SortState,
} from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";

const categorySchema = z.object({
  name: z.string().min(1, "Required"),
  description: z.string(),
});
type CategoryFormValues = z.infer<typeof categorySchema>;

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<WikiCategoryDto | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [sort, setSort] = useState<SortState>({ key: "name", dir: "asc" });
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: categories = [], isPending: loading } = useQuery({
    queryKey: qk.wiki.categories(),
    queryFn: async () => {
      const res = await wikiApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.wiki.categories() });

  const removeMutation = useMutation({
    mutationFn: async (id: number) => { await wikiApi.deleteCategory(id); },
    onSuccess: invalidate,
  });

  const remove = async (id: number) => {
    if (!(await confirm({ message: "Delete this category?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  const filtered = search
    ? categories.filter((c) => c.name.toLowerCase().includes(search) || c.slug.toLowerCase().includes(search))
    : categories;
  const sorted = applySort(filtered, sort, {
    name: (c) => c.name,
    articles: (c) => c.articleCount,
    slug: (c) => c.slug,
  });

  return (
    <div>
      <AdminPageHeader title="Wiki Categories" addLabel="New Category" onAdd={() => setShowForm(true)} />
      <AdminFilterBar search={searchInput} onSearch={setSearchInput} searchPlaceholder="Search categories…" />

      <Modal
        open={showForm}
        onClose={() => { setShowForm(false); setEditing(null); }}
        title={editing ? "Edit Category" : "New Category"}
      >
        <CategoryForm
          initial={editing ?? undefined}
          onDone={() => { setShowForm(false); setEditing(null); invalidate(); }}
          onCancel={() => { setShowForm(false); setEditing(null); }}
        />
      </Modal>

      <AdminTable
        columns={[
          { key: "name", label: "Category", sortable: true },
          { key: "articles", label: "Articles", sortable: true },
          { key: "slug", label: "Slug", sortable: true },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        sort={sort}
        onSortChange={setSort}
        loading={loading}
        empty={filtered.length === 0}
        emptyLabel={categories.length === 0 ? "No categories yet." : "No categories match this search."}
        emptyHint={categories.length === 0 ? "Use New Category to create the first one." : "Try a different search."}
      >
        {sorted.map((c) => (
          <AdminRow key={c.id}>
            <td className="px-3 py-2 font-medium text-fg">{c.name}</td>
            <td className="px-3 py-2 tabular-nums text-fg-muted">{c.articleCount}</td>
            <td className="px-3 py-2 text-fg-subtle">{c.slug}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-1.5">
                <Button size="sm" variant="secondary"
                  onClick={() => { setEditing(c); setShowForm(true); }}>
                  Edit
                </Button>
                <Button size="sm" variant="danger" disabled={c.articleCount > 0}
                  loading={removeMutation.isPending && removeMutation.variables === c.id}
                  onClick={() => remove(c.id)}>
                  Delete
                </Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
    </div>
  );
}

function CategoryForm({ initial, onDone, onCancel }: {
  initial?: WikiCategoryDto; onDone: () => void; onCancel: () => void;
}) {
  const [error, setError] = useState<string | null>(null);
  const {
    register, handleSubmit, watch,
    formState: { errors, isSubmitting },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: initial?.name ?? "", description: initial?.description ?? "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      if (initial) await wikiApi.updateCategory(initial.id, { name: values.name, description: values.description });
      else await wikiApi.createCategory({ name: values.name, description: values.description });
      onDone();
    } catch (err) {
      setError(parseApiError(err, initial ? "Failed to update the category. Please try again." : "Failed to create the category. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Category name" error={errors.name?.message} {...register("name")} />
      <Input label="Description" {...register("description")} />
      <div className="flex gap-2">
        <Button type="submit" loading={isSubmitting} disabled={!watch("name")?.trim()}>
          {initial ? "Save Changes" : "Add Category"}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
