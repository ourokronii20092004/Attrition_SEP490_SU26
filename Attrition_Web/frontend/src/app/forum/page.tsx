"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Plus, Pin, Lock, MessageSquarePlus, MessagesSquare, Search } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { RelativeTime } from "@/components/ui/relative-time";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { useAuth } from "@/lib/providers";
import { qk } from "@/lib/query-keys";

export default function ForumPage() {
  const { user } = useAuth();
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [searchInput, setSearchInput] = useState("");
  const [page, setPage] = useState(1);
  const search = useDebouncedValue(searchInput.trim(), 300);

  const { data: categories = [] } = useQuery({
    queryKey: qk.forum.categories(),
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data ?? [] : [];
    },
  });

  const { data: threads, isPending } = useQuery({
    queryKey: qk.forum.threads({ selectedCategory, search, page }),
    queryFn: async () => {
      const res = await forumApi.getThreads({ category: selectedCategory || undefined, search: search || undefined, page, pageSize: 15 });
      return res.success ? res.data : null;
    },
  });

  const totalPages = threads ? Math.ceil(threads.totalCount / threads.pageSize) : 0;

  return (
    <PageShell>
      <PageTitle
        description="Discuss strategies, lore, and theories with the community."
        actions={
          user && (
            <Link href="/forum/new">
              <Button size="sm"><Plus size={16} className="mr-1.5" /> New Thread</Button>
            </Link>
          )
        }
      >
        Forum
      </PageTitle>

      <div className="flex flex-wrap items-end gap-3">
        <div className="relative min-w-56 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <Input
            value={searchInput}
            onChange={(e) => { setSearchInput(e.target.value); setPage(1); }}
            placeholder="Search threads..."
            className="pl-9"
            aria-label="Search threads"
          />
        </div>
        <div className="w-48">
          <Select
            value={selectedCategory}
            onChange={(e) => {
              setSelectedCategory(e.target.value);
              setPage(1);
            }}
            aria-label="Filter by category"
          >
            <option value="">All categories</option>
            {categories.map((c) => (
              <option key={c.id} value={c.slug}>{c.name}</option>
            ))}
          </Select>
        </div>
      </div>

      {isPending ? (
        <SkeletonList rows={6} className="mt-6" />
      ) : !threads?.items.length ? (
        <EmptyState
          icon={MessagesSquare}
          title="No threads yet"
          description="Be the first to start a discussion in this category."
          action={
            user && (
              <Link href="/forum/new">
                <Button size="sm"><MessageSquarePlus size={16} className="mr-1.5" /> Start a thread</Button>
              </Link>
            )
          }
          className="mt-6"
        />
      ) : (
        <>
          <div className="stagger mt-6 space-y-2.5">
            {threads.items.map((thread, i) => (
              <Card key={thread.id} interactive style={{ "--i": i } as React.CSSProperties} className="p-0">
                <Link href={`/forum/${thread.id}`} className="group flex items-center gap-4 p-4">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      {thread.isPinned && (
                        <span className="inline-flex items-center gap-1 text-xs font-medium text-warning">
                          <Pin size={12} /> Pinned
                        </span>
                      )}
                      {thread.isLocked && (
                        <span className="inline-flex items-center gap-1 text-xs font-medium text-fg-subtle">
                          <Lock size={12} /> Locked
                        </span>
                      )}
                      <h3 className="truncate font-medium text-fg transition-colors group-hover:text-accent">
                        {thread.title}
                      </h3>
                    </div>
                    <p className="mt-1 text-xs text-fg-muted">
                      by {thread.authorName} &middot; {thread.replyCount} replies
                    </p>
                  </div>
                  <span className="shrink-0 text-xs text-fg-subtle">
                    <RelativeTime iso={thread.lastReplyAt} />
                  </span>
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
