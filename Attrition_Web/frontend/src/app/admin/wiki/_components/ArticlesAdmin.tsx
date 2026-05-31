"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import type { WikiArticleListDto } from "@/lib/types";
import { ArticleEditor } from "./ArticleEditor";

export function ArticlesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<WikiArticleListDto | "new" | null>(null);

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

  return (
    <div>
      <div className="mb-4 flex justify-end">
        <Button onClick={() => setEditing("new")}>New Article</Button>
      </div>
      {editing && (
        <ArticleEditor
          article={editing === "new" ? null : editing}
          categories={categories}
          onDone={() => { setEditing(null); invalidate(); }}
          onCancel={() => setEditing(null)}
        />
      )}
      <div className="space-y-2">
        {articles.map((a) => (
          <div key={a.id} className="card flex items-center justify-between p-4">
            <div className="min-w-0">
              <p className="truncate font-medium text-fg">{a.title}</p>
              <p className="text-xs text-fg-muted">{a.categorySlug} · {formatDate(a.updatedAt)}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" variant="secondary" onClick={() => setEditing(a)}>Edit</Button>
              <Button size="sm" variant="danger" onClick={() => remove(a.id)}>Delete</Button>
            </div>
          </div>
        ))}
        {!articles.length && <p className="py-8 text-center text-fg-muted">No articles yet.</p>}
      </div>
    </div>
  );
}
