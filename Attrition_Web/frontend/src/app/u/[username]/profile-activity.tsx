"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { MessagesSquare, BookOpen } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { wikiApi } from "@/lib/api/wiki";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/format-date";
import type { ForumThreadListDto, WikiArticleListDto } from "@/lib/types";

const PAGE_SIZE = 5;
type Tab = "threads" | "articles";

export function ProfileActivity({ userId }: { userId: string; username: string }) {
  const [tab, setTab] = useState<Tab>("threads");

  return (
    <div className="mt-8">
      <div className="mb-4 flex gap-1 border-b border-border">
        <TabButton active={tab === "threads"} onClick={() => setTab("threads")} icon={MessagesSquare}>
          Forum Threads
        </TabButton>
        <TabButton active={tab === "articles"} onClick={() => setTab("articles")} icon={BookOpen}>
          Wiki Contributions
        </TabButton>
      </div>
      {tab === "threads" ? <ThreadsList userId={userId} /> : <ArticlesList userId={userId} />}
    </div>
  );
}

function TabButton({ active, onClick, icon: Icon, children }: {
  active: boolean; onClick: () => void; icon: React.ComponentType<{ size?: number }>; children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      className={`relative flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium transition-colors ${active ? "text-accent" : "text-fg-muted hover:text-fg"}`}
    >
      <Icon size={15} /> {children}
      {active && <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent" />}
    </button>
  );
}

function Pager({ page, total, onPage }: { page: number; total: number; onPage: (p: number) => void }) {
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  if (totalPages <= 1) return null;
  return (
    <div className="mt-4 flex items-center justify-center gap-3">
      <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Prev</Button>
      <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
      <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</Button>
    </div>
  );
}

function ThreadsList({ userId }: { userId: string }) {
  const [items, setItems] = useState<ForumThreadListDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    forumApi.getThreads({ authorId: userId, page, pageSize: PAGE_SIZE })
      .then((res) => { if (!ignore && res.success) { setItems(res.data.items); setTotal(res.data.totalCount); } })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [userId, page]);

  if (loading) return <p className="py-6 text-center text-sm text-fg-muted">Loading…</p>;
  if (!items.length) return <p className="py-6 text-center text-sm text-fg-muted">No forum threads yet.</p>;

  return (
    <>
      <div className="space-y-2">
        {items.map((t) => (
          <Link key={t.id} href={`/forum/${t.id}`}>
            <Card interactive className="flex items-center justify-between p-4">
              <span className="truncate font-medium text-fg">{t.title}</span>
              <span className="ml-3 shrink-0 text-xs text-fg-subtle">{t.replyCount} replies · {formatDate(t.lastReplyAt)}</span>
            </Card>
          </Link>
        ))}
      </div>
      <Pager page={page} total={total} onPage={setPage} />
    </>
  );
}

function ArticlesList({ userId }: { userId: string }) {
  const [items, setItems] = useState<WikiArticleListDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    wikiApi.getArticles({ authorId: userId, page, pageSize: PAGE_SIZE })
      .then((res) => { if (!ignore && res.success) { setItems(res.data.items); setTotal(res.data.totalCount); } })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [userId, page]);

  if (loading) return <p className="py-6 text-center text-sm text-fg-muted">Loading…</p>;
  if (!items.length) return <p className="py-6 text-center text-sm text-fg-muted">No wiki contributions yet.</p>;

  return (
    <>
      <div className="space-y-2">
        {items.map((a) => (
          <Link key={a.id} href={`/wiki/${a.slug}`}>
            <Card interactive className="flex items-center justify-between p-4">
              <span className="truncate font-medium text-fg">{a.title}</span>
              <span className="ml-3 shrink-0 text-xs text-fg-subtle">{formatDate(a.updatedAt)}</span>
            </Card>
          </Link>
        ))}
      </div>
      <Pager page={page} total={total} onPage={setPage} />
    </>
  );
}
