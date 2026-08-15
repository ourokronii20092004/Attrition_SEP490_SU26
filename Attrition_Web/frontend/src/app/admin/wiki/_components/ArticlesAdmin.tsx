"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import {
  AdminPageHeader, AdminFilterBar, AdminTable, AdminRow, AdminSelectCell, AdminBulkBar, applySort,
  type SortState,
} from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { useUrlPagination } from "@/lib/hooks/use-url-pagination";
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
  const [statusFilter, setStatusFilter] = useState("all");
  const [sort, setSort] = useState<SortState>({ key: "updated", dir: "desc" });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: articles = [], isPending: articlesLoading } = useQuery({
    queryKey: qk.admin.wiki.articles(),
    queryFn: async () => {
      // includeDrafts so drafts are visible here — they can't be published/edited/deleted otherwise.
      const res = await wikiApi.getArticles({ pageSize: 100, includeDrafts: true });
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

  const setStatusMutation = useMutation({
    mutationFn: async ({ id, status }: { id: string; status: string }) => { await wikiApi.updateArticle(id, { status }); },
    onSuccess: invalidate,
  });

  const remove = async (id: string) => {
    if (!(await confirm({ message: "Delete this article?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  const handleBulkDelete = async () => {
    const ids = [...selected];
    if (ids.length === 0) return;
    const ok = await confirm({
      title: `Delete ${ids.length} article${ids.length === 1 ? "" : "s"}?`,
      message: "This permanently removes the selected articles. It can't be undone.",
      danger: true,
      confirmLabel: `Delete ${ids.length}`,
    });
    if (!ok) return;
    await Promise.allSettled(ids.map((id) => wikiApi.deleteArticle(id)));
    setSelected(new Set());
    invalidate();
  };

  const filtered = articles.filter((a) => {
    if (categoryFilter !== "all" && a.categorySlug !== categoryFilter) return false;
    if (statusFilter !== "all" && a.status !== statusFilter) return false;
    if (search && !a.title.toLowerCase().includes(search)) return false;
    return true;
  });
  const sorted = applySort(filtered, sort, {
    title: (a) => a.title,
    category: (a) => a.categorySlug,
    updated: (a) => a.updatedAt,
  });
  const { page, setPage, totalPages, paged } = useUrlPagination(sorted, 20);

  const selection = {
    selected,
    onChange: setSelected,
    pageIds: paged.map((a) => a.id),
  };

  return (
    <div>
      <AdminPageHeader title="Wiki Articles" addLabel="New Article" onAdd={() => setEditing("new")}>
        <AdminBulkBar count={selected.size} onClear={() => setSelected(new Set())}>
          <Button size="sm" variant="danger" onClick={handleBulkDelete}>Delete</Button>
        </AdminBulkBar>
      </AdminPageHeader>
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search articles…"
        filters={[
          {
            value: categoryFilter, onChange: setCategoryFilter, ariaLabel: "Filter by category",
            options: [{ value: "all", label: "All categories" }, ...categories.map((c) => ({ value: c.slug, label: c.name }))],
          },
          {
            value: statusFilter, onChange: setStatusFilter, ariaLabel: "Filter by status",
            options: [{ value: "all", label: "All statuses" }, { value: "Published", label: "Published" }, { value: "Draft", label: "Draft" }],
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
          { key: "title", label: "Title", sortable: true },
          { key: "category", label: "Category", sortable: true },
          { key: "updated", label: "Updated", sortable: true },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        sort={sort}
        onSortChange={setSort}
        selection={selection}
        loading={articlesLoading || categoriesLoading}
        empty={filtered.length === 0}
        emptyLabel={articles.length === 0 ? "No articles yet." : "No articles match these filters."}
        emptyHint={articles.length === 0 ? "Use New Article to write the first one." : "Try a different search or category."}
      >
        {paged.map((a) => (
          <AdminRow key={a.id} onClick={() => setEditing(a)} selected={selected.has(a.id)}>
            <AdminSelectCell id={a.id} selection={selection} />
            <td className="px-3 py-2">
              {a.status === "Draft" && <span className="mr-1 text-xs font-medium text-warning">[Draft]</span>}
              <span className="font-medium text-fg">{a.title}</span>
            </td>
            <td className="px-3 py-2 text-fg-muted">{a.categorySlug}</td>
            <td className="px-3 py-2 text-fg-muted">{formatDate(a.updatedAt)}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); setStatusMutation.mutate({ id: a.id, status: a.status === "Draft" ? "Published" : "Draft" }); }}>
                  {a.status === "Draft" ? "Publish" : "Unpublish"}
                </Button>
                <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); setEditing(a); }}>Edit</Button>
                <Button size="sm" variant="danger" onClick={(e) => { e.stopPropagation(); remove(a.id); }}>Delete</Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
    </div>
  );
}
