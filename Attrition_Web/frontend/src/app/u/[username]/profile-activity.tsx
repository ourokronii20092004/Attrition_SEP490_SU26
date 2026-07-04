"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { clsx } from "clsx";
import { MessagesSquare, BookOpen, ArrowRight } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RelativeTime } from "@/components/ui/relative-time";
import type { ForumThreadListDto, WikiArticleListDto } from "@/lib/types";

const PAGE_SIZE = 5;
type Tab = "threads" | "articles";

export function ProfileActivity({ userId, username }: { userId: string; username: string }) {
  const [tab, setTab] = useState<Tab>("threads");

  return (
    <section className="mt-12">
      <div className="mb-5 flex flex-col gap-4 border-b border-border pb-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="font-mono text-[11px] uppercase tracking-[0.25em] text-accent">Activity</p>
          <h2 className="mt-1 font-display text-2xl font-semibold tracking-tight text-fg">Contributions</h2>
        </div>
        <div className="inline-flex self-start rounded-lg border border-border bg-surface-2/60 p-1 sm:self-auto">
          <SegTab active={tab === "threads"} onClick={() => setTab("threads")} icon={MessagesSquare}>Threads</SegTab>
          <SegTab active={tab === "articles"} onClick={() => setTab("articles")} icon={BookOpen}>Wiki</SegTab>
        </div>
      </div>
      {tab === "threads" ? <ThreadsList userId={userId} username={username} /> : <ArticlesList userId={userId} username={username} />}
    </section>
  );
}

function SegTab({ active, onClick, icon: Icon, children }: {
  active: boolean; onClick: () => void; icon: React.ComponentType<{ size?: number }>; children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      className={clsx(
        "inline-flex items-center gap-1.5 rounded-md px-3.5 py-1.5 text-sm font-medium transition-[background-color,color,box-shadow] duration-200",
        active ? "bg-accent text-accent-fg shadow-sm" : "text-fg-muted hover:text-fg",
      )}
    >
      <Icon size={15} /> {children}
    </button>
  );
}

/** Shared numbered activity row: mono index, icon chip, title + meta, hover accent line + arrow. */
function ActivityRow({ index, href, icon: Icon, title, meta }: {
  index: number; href: string; icon: React.ComponentType<{ size?: number; className?: string }>;
  title: string; meta: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className="group relative flex items-center gap-3 overflow-hidden rounded-card border border-border bg-surface px-4 py-3.5 transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-0.5 hover:border-accent/60 hover:shadow-[var(--shadow-glow)] sm:gap-4"
    >
      <span aria-hidden className="pointer-events-none absolute inset-y-3 left-0 w-px origin-top scale-y-0 bg-accent transition-transform duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] group-hover:scale-y-100" />
      <span className="font-mono text-xs tabular-nums text-fg-subtle transition-colors group-hover:text-accent">
        {String(index).padStart(2, "0")}
      </span>
      <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors group-hover:border-accent/40 group-hover:text-accent">
        <Icon size={16} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium text-fg transition-colors group-hover:text-accent">{title}</span>
        <span className="mt-0.5 block text-xs text-fg-subtle">{meta}</span>
      </span>
      <ArrowRight size={16} className="shrink-0 -translate-x-2 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100" />
    </Link>
  );
}

function RowsSkeleton() {
  return (
    <div className="space-y-2" aria-busy="true" aria-label="Loading">
      {Array.from({ length: 4 }).map((_, i) => (
        <Skeleton key={i} className="h-[4.25rem] w-full rounded-card" />
      ))}
    </div>
  );
}

function Pager({ page, total, onPage }: { page: number; total: number; onPage: (p: number) => void }) {
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  if (totalPages <= 1) return null;
  return (
    <div className="mt-5 flex items-center justify-center gap-3">
      <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Prev</Button>
      <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
      <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</Button>
    </div>
  );
}

function ThreadsList({ userId, username }: { userId: string; username: string }) {
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

  if (loading) return <RowsSkeleton />;
  if (!items.length) {
    return <EmptyState icon={MessagesSquare} title="No forum threads yet" description={`@${username} hasn't started any discussions.`} />;
  }

  return (
    <>
      <div className="space-y-2">
        {items.map((t, i) => (
          <ActivityRow
            key={t.id}
            index={(page - 1) * PAGE_SIZE + i + 1}
            href={`/forum/${t.id}`}
            icon={MessagesSquare}
            title={t.title}
            meta={<>{t.replyCount} {t.replyCount === 1 ? "reply" : "replies"} · <RelativeTime iso={t.lastReplyAt} /></>}
          />
        ))}
      </div>
      <Pager page={page} total={total} onPage={setPage} />
    </>
  );
}

function ArticlesList({ userId, username }: { userId: string; username: string }) {
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

  if (loading) return <RowsSkeleton />;
  if (!items.length) {
    return <EmptyState icon={BookOpen} title="No wiki contributions yet" description={`@${username} hasn't authored any articles.`} />;
  }

  return (
    <>
      <div className="space-y-2">
        {items.map((a, i) => (
          <ActivityRow
            key={a.id}
            index={(page - 1) * PAGE_SIZE + i + 1}
            href={`/wiki/${a.slug}`}
            icon={BookOpen}
            title={a.title}
            meta={<>Updated <RelativeTime iso={a.updatedAt} /></>}
          />
        ))}
      </div>
      <Pager page={page} total={total} onPage={setPage} />
    </>
  );
}
