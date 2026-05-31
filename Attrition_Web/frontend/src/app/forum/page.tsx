"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Plus, Pin, Lock, MessageSquarePlus, MessagesSquare } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { FilterPills } from "@/components/ui/filter-pills";
import { RelativeTime } from "@/components/ui/relative-time";
import { useAuth } from "@/lib/providers";

export default function ForumPage() {
  const { user } = useAuth();
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const { data: categories = [] } = useQuery({
    queryKey: ["forum", "categories"],
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data ?? [] : [];
    },
  });

  const { data: threads, isPending } = useQuery({
    queryKey: ["forum", "threads", { selectedCategory, page }],
    queryFn: async () => {
      const res = await forumApi.getThreads({ categoryId: selectedCategory ?? undefined, page, pageSize: 15 });
      return res.success ? res.data : null;
    },
  });

  const totalPages = threads ? Math.ceil(threads.totalCount / threads.pageSize) : 0;

  const pillOptions = [
    { value: null as number | null, label: "All" },
    ...categories.map((c) => ({ value: c.id as number | null, label: c.name })),
  ];

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

      <FilterPills
        options={pillOptions}
        value={selectedCategory}
        onChange={(v) => { setSelectedCategory(v); setPage(1); }}
      />

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
