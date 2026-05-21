import Link from "next/link";
import styles from "../forum.module.css";
import { NewThreadButton } from "../NewThreadButton";

export const dynamic = "force-dynamic";

interface ForumThread {
  id: string;
  title: string;
  categorySlug: string;
  authorName: string;
  authorAvatar: string | null;
  createdAt: string;
  replyCount: number;
  isPinned: boolean;
  isLocked: boolean;
  lastReplyAt: string | null;
}

function timeAgo(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return new Date(dateString).toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

import { Pagination } from "@/components/Pagination";

async function getThreads(categorySlug: string, page: number): Promise<{ items: ForumThread[], totalCount: number, pageSize: number }> {
  try {
    const res = await fetch(
      `${process.env.API_URL || "http://localhost:5000"}/api/forum/threads?category=${encodeURIComponent(categorySlug)}&page=${page}&pageSize=15`,
      { cache: "no-store" }
    );
    if (!res.ok) return { items: [], totalCount: 0, pageSize: 15 };
    const data = await res.json();
    if (data.items && Array.isArray(data.items)) return data;
    if (Array.isArray(data)) return { items: data, totalCount: data.length, pageSize: 15 };
    if (data.success && data.data && data.data.items) return data.data;
    return { items: [], totalCount: 0, pageSize: 15 };
  } catch {
    return { items: [], totalCount: 0, pageSize: 15 };
  }
}

export default async function ForumCategoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { slug } = await params;
  const sp = await searchParams;
  const page = parseInt(sp.page as string) || 1;
  const { items: threads, totalCount, pageSize } = await getThreads(slug, page);
  const categoryName = slug.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());

  // Sort: pinned first, then by lastActivityAt
  const sorted = [...threads].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1;
    const aTime = new Date(a.lastReplyAt || a.createdAt).getTime();
    const bTime = new Date(b.lastReplyAt || b.createdAt).getTime();
    return bTime - aTime;
  });

  return (
    <div className="page">
      <div className="container">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <Link href="/forum">Forum</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">{categoryName}</span>
          </div>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-4)" }}>
            <div>
              <h1>{categoryName}</h1>
              <p>{threads.length} {threads.length === 1 ? "thread" : "threads"}</p>
            </div>
            <NewThreadButton />
          </div>
        </div>

        {sorted.length > 0 ? (
          <div className={styles.threadList}>
            {sorted.map((thread) => (
              <Link
                key={thread.id}
                href={`/forum/thread/${thread.id}`}
                className={`${styles.threadRow} ${thread.isPinned ? styles.threadPinned : ""}`}
              >
                <span className={styles.threadIcon}>
                  {thread.isPinned ? "📌" : thread.isLocked ? "🔒" : "💬"}
                </span>
                <div className={styles.threadInfo}>
                  <h3 className={styles.threadTitle}>{thread.title}</h3>
                  <div className={styles.threadMeta}>
                    <span>{thread.authorName}</span>
                    <span>·</span>
                    <span>{timeAgo(thread.createdAt)}</span>
                  </div>
                </div>
                <span className={styles.threadReplies}>
                  {thread.replyCount} {thread.replyCount === 1 ? "reply" : "replies"}
                </span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <span className="empty-state-icon">💬</span>
            <h3>No threads yet</h3>
            <p>Be the first to start a discussion in this category.</p>
          </div>
        )}

        <Pagination 
          currentPage={page} 
          totalCount={totalCount} 
          pageSize={pageSize} 
          baseUrl={`/forum/${slug}`} 
        />
      </div>
    </div>
  );
}
