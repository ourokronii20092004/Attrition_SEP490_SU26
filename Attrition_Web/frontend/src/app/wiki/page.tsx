"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { wikiApi } from "@/lib/api/wiki";
import { PageLoader } from "@/components/ui/spinner";
import type { WikiCategoryDto, WikiArticleListDto, PaginatedResponse } from "@/lib/types";

export default function WikiPage() {
  const [categories, setCategories] = useState<WikiCategoryDto[]>([]);
  const [articles, setArticles] = useState<PaginatedResponse<WikiArticleListDto> | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const totalPages = articles ? Math.ceil(articles.totalCount / articles.pageSize) : 0;

  useEffect(() => {
    wikiApi.getCategories().then((res) => {
      if (res.success) setCategories(res.data);
    });
  }, []);

  useEffect(() => {
    setLoading(true);
    wikiApi
      .getArticles({ category: selectedCategory || undefined, search: search || undefined, page, pageSize: 12 })
      .then((res) => {
        if (res.success) setArticles(res.data);
      })
      .finally(() => setLoading(false));
  }, [selectedCategory, search, page]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Wiki</h1>
      <p className="mt-2 text-fg-muted">Explore the lore and knowledge of Attrition</p>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <input
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          placeholder="Search articles..."
          className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
        />
        <select
          value={selectedCategory}
          onChange={(e) => { setSelectedCategory(e.target.value); setPage(1); }}
          className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none"
        >
          <option value="">All Categories</option>
          {categories.map((c) => (
            <option key={c.id} value={c.slug}>{c.name}</option>
          ))}
        </select>
      </div>

      {loading ? (
        <PageLoader />
      ) : (
        <>
          <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {articles?.items.map((article) => (
              <Link
                key={article.id}
                href={`/wiki/${article.slug}`}
                className="card p-4 transition hover:border-accent"
              >
                <h3 className="font-medium text-fg">{article.title}</h3>
                <p className="mt-1 text-xs text-fg-muted">{article.categorySlug}</p>
                <p className="mt-2 text-xs text-fg-subtle">by {article.authorName ?? "Unknown"}</p>
              </Link>
            ))}
          </div>

          {articles && totalPages > 1 && (
            <div className="mt-8 flex items-center justify-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50"
              >
                Prev
              </button>
              <span className="text-sm text-fg-muted">
                {page} / {totalPages}
              </span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
