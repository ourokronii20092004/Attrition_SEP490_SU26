"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import type { WikiArticleListDto } from "@/lib/types";
import { ArticleEditor } from "./ArticleEditor";

export function ArticlesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<WikiArticleListDto | "new" | null>(null);
  const [formDirty, setFormDirty] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("all");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: articles = [], isPending: articlesLoading } = useQuery({
    queryKey: qk.admin.wiki.articles(),
    queryFn: async () => {
      const res = await wikiApi.getArticles({ pageSize: 100 });
      return res.success ? res.data.items : [];
    },
  });

  const { data: categories = [], isPending: categoriesLoading } = useQuery({
    queryKey: qk.wiki.categories(),
    queryFn: async () => {
      const res = await wikiApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.wiki.articles() });

  const removeMutation = useMutation({
    mutationFn: async (id: string) => { await wikiApi.deleteArticle(id); },
    onSuccess: invalidate,
  });

  const remove = async (id: string) => {
    if (!(await confirm({ message: "Delete this article?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  if (articlesLoading || categoriesLoading) return <PageLoader />;

  const filtered = articles.filter((a) => {
    if (categoryFilter !== "all" && a.categorySlug !== categoryFilter) return false;
    if (search && !a.title.toLowerCase().includes(search)) return false;
    return true;
  });

  return (
    <div>
      <AdminPageHeader title="Wiki Articles" addLabel="New Article" onAdd={() => setEditing("new")} />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search articles…"
        filters={[
          {
            value: categoryFilter, onChange: setCategoryFilter, ariaLabel: "Filter by category",
            options: [{ value: "all", label: "All categories" }, ...categories.map((c) => ({ value: c.slug, label: c.name }))],
          },
        ]}
      />

      <Modal
        open={editing != null}
        onClose={() => setEditing(null)}
        title={editing === "new" ? "New Article" : "Edit Article"}
        size="lg"
        dirty={formDirty}
      >
        {editing && (
          <ArticleEditor
            article={editing === "new" ? null : editing}
            categories={categories}
            onDirtyChange={setFormDirty}
            onDone={() => { setFormDirty(false); setEditing(null); invalidate(); }}
            onCancel={() => { setFormDirty(false); setEditing(null); }}
          />
        )}
      </Modal>

      <AdminTable
        columns={[
          { key: "title", label: "Title" },
          { key: "category", label: "Category" },
          { key: "updated", label: "Updated" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        empty={filtered.length === 0}
      >
        {filtered.map((a) => (
          <AdminRow key={a.id} onClick={() => setEditing(a)}>
            <td className="px-3 py-2 font-medium text-fg">{a.title}</td>
            <td className="px-3 py-2 text-fg-muted">{a.categorySlug}</td>
            <td className="px-3 py-2 text-fg-muted">{formatDate(a.updatedAt)}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); setEditing(a); }}>Edit</Button>
                <Button size="sm" variant="danger" onClick={(e) => { e.stopPropagation(); remove(a.id); }}>Delete</Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
    </div>
  );
}
