"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import type { ForumCategoryDto } from "@/lib/types";
import { parseApiError } from "@/lib/api/parse-error";
import { useConfirm, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import {
  AdminPageHeader, AdminFilterBar, AdminTable, AdminRow, AdminSelectCell, AdminBulkBar, applySort,
  type SortState,
} from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { useUrlPagination } from "@/lib/hooks/use-url-pagination";
import { qk } from "@/lib/query-keys";

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const router = useRouter();
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<ForumCategoryDto | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [sort, setSort] = useState<SortState>({ key: "name", dir: "asc" });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: categories = [], isPending: loading } = useQuery({
    queryKey: qk.forum.categories(),
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.forum.categories() });

  const deleteMutation = useMutation({
    mutationFn: async (id: number) => { await forumApi.deleteCategory(id); },
    onSuccess: () => { toast("Category deleted.", "success"); invalidate(); },
    onError: (err) => toast(parseApiError(err, "Could not delete the category."), "error"),
  });

  const remove = async (id: number, label: string) => {
    if (!(await confirm({ message: `Delete the "${label}" category?`, danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
  };

  const handleBulkDelete = async () => {
    const ids = [...selected];
    if (ids.length === 0) return;
    const ok = await confirm({
      title: `Delete ${ids.length} categor${ids.length === 1 ? "y" : "ies"}?`,
      message: "This permanently removes the selected categories. It can't be undone.",
      danger: true,
      confirmLabel: `Delete ${ids.length}`,
    });
    if (!ok) return;
    await Promise.allSettled(ids.map((id) => forumApi.deleteCategory(Number(id))));
    setSelected(new Set());
    invalidate();
  };

  const filtered = search
    ? categories.filter((c) => c.name.toLowerCase().includes(search) || c.slug.toLowerCase().includes(search))
    : categories;
  const sorted = applySort(filtered, sort, {
    name: (c) => c.name,
    threads: (c) => c.threadCount,
    slug: (c) => c.slug,
  });
  const { page, setPage, totalPages, paged } = useUrlPagination(sorted, 20);

  const selection = {
    selected,
    onChange: setSelected,
    pageIds: paged.map((c) => String(c.id)),
  };

  return (
    <div>
      <AdminPageHeader title="Forum Categories" addLabel="New Category" onAdd={() => setShowForm(true)}>
        <AdminBulkBar count={selected.size} onClear={() => setSelected(new Set())}>
          <Button size="sm" variant="danger" onClick={handleBulkDelete}>Delete</Button>
        </AdminBulkBar>
      </AdminPageHeader>
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
          { key: "threads", label: "Threads", align: "right", sortable: true },
          { key: "slug", label: "Slug", sortable: true },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        sort={sort}
        onSortChange={setSort}
        selection={selection}
        loading={loading}
        empty={filtered.length === 0}
        emptyLabel={categories.length === 0 ? "No categories yet." : "No categories match this search."}
        emptyHint={categories.length === 0 ? "Use New Category to create the first one." : "Try a different search."}
      >
        {paged.map((c) => (
          <AdminRow
            key={c.id}
            onClick={() => router.push(`/admin/forum/categories/${c.id}`)}
            selected={selected.has(String(c.id))}
          >
            <AdminSelectCell id={String(c.id)} selection={selection} />
            <td className="px-3 py-2 font-medium text-fg">{c.name}</td>
            <td className="px-3 py-2 text-right tabular-nums text-fg-muted">{c.threadCount}</td>
            <td className="px-3 py-2 text-fg-subtle">{c.slug}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-1.5">
                <Button size="sm" variant="secondary"
                  onClick={(e) => { e.stopPropagation(); setEditing(c); setShowForm(true); }}>
                  Edit
                </Button>
                <Button size="sm" variant="danger"
                  loading={deleteMutation.isPending && deleteMutation.variables === c.id}
                  onClick={(e) => { e.stopPropagation(); remove(c.id, c.name); }}>
                  Delete
                </Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
    </div>
  );
}

function CategoryForm({ initial, onDone, onCancel }: {
  initial?: ForumCategoryDto; onDone: () => void; onCancel: () => void;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      if (initial) await forumApi.updateCategory(initial.id, { name, description });
      else await forumApi.createCategory({ name, description });
    },
    onSuccess: onDone,
    onError: (err) => setError(parseApiError(err, initial ? "Failed to update the category." : "Failed to create the category.")),
  });

  return (
    <div className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Category name" value={name} onChange={(e) => setName(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <div className="flex gap-2">
        <Button onClick={() => mutation.mutate()} loading={mutation.isPending} disabled={!name.trim()}>
          {initial ? "Save Changes" : "Add Category"}
        </Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
