"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { forumApi } from "@/lib/api/forum";
import { PageLoader } from "@/components/ui/spinner";
import { useAuth } from "@/lib/providers";
import type { ForumCategoryDto, ForumThreadListDto, PaginatedResponse } from "@/lib/types";

export default function ForumPage() {
  const { user } = useAuth();
  const [categories, setCategories] = useState<ForumCategoryDto[]>([]);
  const [threads, setThreads] = useState<PaginatedResponse<ForumThreadListDto> | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const totalPages = threads ? Math.ceil(threads.totalCount / threads.pageSize) : 0;

  useEffect(() => {
    forumApi.getCategories().then((res) => {
      if (res.success) setCategories(res.data);
    });
  }, []);

  useEffect(() => {
    setLoading(true);
    forumApi
      .getThreads({ categoryId: selectedCategory ?? undefined, page, pageSize: 15 })
      .then((res) => {
        if (res.success) setThreads(res.data);
      })
      .finally(() => setLoading(false));
  }, [selectedCategory, page]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-4xl font-bold text-fg">Forum</h1>
          <p className="mt-2 text-fg-muted">Discuss strategies, lore, and more</p>
        </div>
        {user && (
          <Link href="/forum/new" className="rounded-lg bg-accent px-4 py-2 text-sm font-medium text-accent-fg hover:opacity-90">
            New Thread
          </Link>
        )}
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        <button
          onClick={() => { setSelectedCategory(null); setPage(1); }}
          className={`rounded-md px-3 py-1.5 text-sm ${selectedCategory === null ? "bg-accent text-accent-fg" : "border border-border text-fg-muted hover:bg-surface-2"}`}
        >
          All
        </button>
        {categories.map((c) => (
          <button
            key={c.id}
            onClick={() => { setSelectedCategory(c.id); setPage(1); }}
            className={`rounded-md px-3 py-1.5 text-sm ${selectedCategory === c.id ? "bg-accent text-accent-fg" : "border border-border text-fg-muted hover:bg-surface-2"}`}
          >
            {c.name}
          </button>
        ))}
      </div>

      {loading ? (
        <PageLoader />
      ) : (
        <>
          <div className="mt-6 space-y-2">
            {threads?.items.map((thread) => (
              <Link
                key={thread.id}
                href={`/forum/${thread.id}`}
                className="card flex items-center gap-4 p-4 transition hover:border-accent"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    {thread.isPinned && <span className="text-xs font-medium text-warning">Pinned</span>}
                    {thread.isLocked && <span className="text-xs font-medium text-fg-subtle">Locked</span>}
                    <h3 className="truncate font-medium text-fg">{thread.title}</h3>
                  </div>
                  <p className="mt-1 text-xs text-fg-muted">
                    by {thread.authorName} &middot; {thread.replyCount} replies
                  </p>
                </div>
                <span className="shrink-0 text-xs text-fg-subtle">
                  {new Date(thread.lastReplyAt).toLocaleDateString()}
                </span>
              </Link>
            ))}
            {threads?.items.length === 0 && <p className="py-8 text-center text-fg-muted">No threads yet.</p>}
          </div>

          {totalPages > 1 && (
            <div className="mt-8 flex items-center justify-center gap-2">
              <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
              <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
              <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
