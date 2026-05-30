"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Search, BookOpen } from "lucide-react";
import { wikiApi } from "@/lib/api/wiki";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
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
      if (res.success) setCategories(res.data ?? []);
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
    <PageShell>
      <PageTitle description="Explore the lore and knowledge of the Attrition universe.">
        Wiki
      </PageTitle>

      <div className="flex flex-wrap items-end gap-3">
        <div className="relative min-w-56 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <Input
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="Search articles..."
            className="pl-9"
            aria-label="Search articles"
          />
        </div>
        <div className="w-48">
          <Select
            value={selectedCategory}
            onChange={(e) => { setSelectedCategory(e.target.value); setPage(1); }}
            aria-label="Filter by category"
          >
            <option value="">All Categories</option>
            {categories.map((c) => (
              <option key={c.id} value={c.slug}>{c.name}</option>
            ))}
          </Select>
        </div>
      </div>

      {loading ? (
        <SkeletonGrid count={6} className="mt-6 lg:grid-cols-3" />
      ) : !articles?.items.length ? (
        <EmptyState
          icon={BookOpen}
          title="No articles found"
          description={search || selectedCategory ? "Try a different search or category." : "Articles will appear here once they're published."}
          className="mt-6"
        />
      ) : (
        <>
          <div className="stagger mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {articles.items.map((article, i) => (
              <Card key={article.id} interactive style={{ "--i": i } as React.CSSProperties} className="p-0">
                <Link href={`/wiki/${article.slug}`} className="group block p-5">
                  <span className="text-xs font-medium uppercase tracking-wider text-accent">
                    {article.categorySlug}
                  </span>
                  <h3 className="mt-2 font-display text-lg font-semibold text-fg transition-colors group-hover:text-accent">
                    {article.title}
                  </h3>
                  <p className="mt-3 text-xs text-fg-subtle">by {article.authorName ?? "Unknown"}</p>
                </Link>
              </Card>
            ))}
          </div>
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </>
      )}
    </PageShell>
  );
}
