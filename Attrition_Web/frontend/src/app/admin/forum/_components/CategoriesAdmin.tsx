"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { parseApiError } from "@/lib/api/parse-error";
import { useConfirm, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const router = useRouter();
  const [showForm, setShowForm] = useState(false);
  const [searchInput, setSearchInput] = useState("");
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

  const filtered = search
    ? categories.filter((c) => c.name.toLowerCase().includes(search) || c.slug.toLowerCase().includes(search))
    : categories;

  return (
    <div>
      <AdminPageHeader title="Forum Categories" addLabel="New Category" onAdd={() => setShowForm(true)} />
      <AdminFilterBar search={searchInput} onSearch={setSearchInput} searchPlaceholder="Search categories…" />

      <Modal open={showForm} onClose={() => setShowForm(false)} title="New Category">
        <CategoryForm onDone={() => { setShowForm(false); invalidate(); }} onCancel={() => setShowForm(false)} />
      </Modal>

      {loading ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "name", label: "Category" },
            { key: "threads", label: "Threads" },
            { key: "slug", label: "Slug" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={filtered.length === 0}
        >
          {filtered.map((c) => (
            <AdminRow key={c.id} onClick={() => router.push(`/admin/forum/categories/${c.id}`)}>
              <td className="px-3 py-2 font-medium text-fg">{c.name}</td>
              <td className="px-3 py-2 tabular-nums text-fg-muted">{c.threadCount}</td>
              <td className="px-3 py-2 text-fg-subtle">{c.slug}</td>
              <td className="px-3 py-2 text-right">
                <Button size="sm" variant="danger"
                  loading={deleteMutation.isPending && deleteMutation.variables === c.id}
                  onClick={(e) => { e.stopPropagation(); remove(c.id, c.name); }}>
                  Delete
                </Button>
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}
    </div>
  );
}

function CategoryForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: async () => { await forumApi.createCategory({ name, description }); },
    onSuccess: onDone,
    onError: (err) => setError(parseApiError(err, "Failed to create the category.")),
  });

  return (
    <div className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Category name" value={name} onChange={(e) => setName(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <div className="flex gap-2">
        <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending} disabled={!name.trim()}>Add Category</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
