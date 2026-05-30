"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, ThumbsUp, ThumbsDown, Flag, Lock } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { SkeletonList, Skeleton } from "@/components/ui/skeleton";
import { Pagination } from "@/components/ui/pagination";
import { RelativeTime } from "@/components/ui/relative-time";
import type { ForumThreadDto, ForumPostDto, PaginatedResponse } from "@/lib/types";

export default function ThreadPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();
  const [thread, setThread] = useState<ForumThreadDto | null>(null);
  const [posts, setPosts] = useState<PaginatedResponse<ForumPostDto> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [replyContent, setReplyContent] = useState("");
  const [replying, setReplying] = useState(false);
  const [actionError, setActionError] = useState("");
  const [reportingId, setReportingId] = useState<string | null>(null);
  const totalPages = posts ? Math.ceil(posts.totalCount / posts.pageSize) : 0;

  useEffect(() => {
    if (!params.id) return;
    let ignore = false;
    forumApi.getThread(params.id).then((res) => {
      if (!ignore && res.success) setThread(res.data);
    });
    return () => { ignore = true; };
  }, [params.id]);

  useEffect(() => {
    if (!params.id) return;
    let ignore = false;
    setLoading(true);
    forumApi
      .getPosts(params.id, { page, pageSize: 20 })
      .then((res) => {
        if (!ignore && res.success) setPosts(res.data);
      })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [params.id, page]);

  const handleReply = async () => {
    if (!replyContent.trim() || !params.id) return;
    setReplying(true);
    setActionError("");
    try {
      await forumApi.createPost(params.id, { content: replyContent });
      setReplyContent("");
      const res = await forumApi.getPosts(params.id, { page, pageSize: 20 });
      if (res.success) setPosts(res.data);
    } catch {
      setActionError("Failed to post reply. Please try again.");
    }
    setReplying(false);
  };

  const handleReact = async (postId: string, type: "like" | "dislike") => {
    setActionError("");
    try {
      await forumApi.react(postId, { reactionType: type });
      const res = await forumApi.getPosts(params.id, { page, pageSize: 20 });
      if (res.success) setPosts(res.data);
    } catch {
      setActionError("Failed to register your reaction. Please try again.");
    }
  };

  const handleReport = async (postId: string) => {
    const reason = window.prompt("Why are you reporting this post?");
    if (!reason?.trim()) return;
    setReportingId(postId);
    setActionError("");
    try {
      await forumApi.report(postId, { reason: reason.trim() });
      window.alert("Report submitted. Thank you.");
    } catch {
      setActionError("Failed to submit report. Please try again.");
    }
    setReportingId(null);
  };

  if (loading && !thread) {
    return (
      <PageShell size="lg">
        <Skeleton className="h-4 w-16" />
        <Skeleton className="mt-4 h-9 w-2/3" />
        <SkeletonList rows={4} className="mt-6" />
      </PageShell>
    );
  }

  return (
    <PageShell size="lg">
      <Link href="/forum" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Forum
      </Link>

      {thread && (
        <div className="mt-4">
          <h1 className="font-display text-2xl font-bold tracking-tight text-balance text-fg sm:text-3xl">
            {thread.title}
          </h1>
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-fg-muted">
            <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-xs font-medium text-accent">
              {thread.categorySlug}
            </span>
            <span>by {thread.authorName}</span>
            <span className="text-fg-subtle">&middot;</span>
            <span>{thread.replyCount} replies</span>
            {thread.isLocked && (
              <span className="inline-flex items-center gap-1 text-xs font-medium text-fg-subtle">
                <Lock size={12} /> Locked
              </span>
            )}
          </div>
        </div>
      )}

      {loading ? (
        <SkeletonList rows={4} className="mt-6" />
      ) : (
        <div className="mt-6 space-y-3">
          {posts?.items.map((post) => (
            <Card key={post.id} className="p-4">
              <div className="flex items-start gap-3">
                <Avatar src={post.authorAvatar} name={post.authorName} size="md" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link href={`/u/${encodeURIComponent(post.authorName)}`} className="text-sm font-medium text-fg transition-colors hover:text-accent">
                      {post.authorName}
                    </Link>
                    {post.authorRole === "Admin" && (
                      <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>
                    )}
                    <span className="text-xs text-fg-subtle"><RelativeTime iso={post.createdAt} /></span>
                  </div>
                  <div className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-fg">{post.content}</div>

                  <div className="mt-3 flex items-center gap-1">
                    <button
                      onClick={() => handleReact(post.id, "like")}
                      className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${post.currentUserReaction === "like" ? "bg-accent-soft text-accent" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}
                    >
                      <ThumbsUp size={14} /> {post.likeCount}
                    </button>
                    <button
                      onClick={() => handleReact(post.id, "dislike")}
                      className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${post.currentUserReaction === "dislike" ? "bg-danger/10 text-danger" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}
                    >
                      <ThumbsDown size={14} /> {post.dislikeCount}
                    </button>
                    {user && (
                      <button
                        onClick={() => handleReport(post.id)}
                        disabled={reportingId === post.id}
                        className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-warning disabled:opacity-50"
                      >
                        <Flag size={14} /> Report
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      {actionError && <p className="mt-4 text-sm text-danger">{actionError}</p>}

      <Pagination page={page} totalPages={totalPages} onChange={setPage} />

      {user && thread && !thread.isLocked && (
        <Card className="mt-6 p-4">
          <label htmlFor="reply" className="text-sm font-medium text-fg">Write a reply</label>
          <textarea
            id="reply"
            value={replyContent}
            onChange={(e) => setReplyContent(e.target.value)}
            placeholder="Share your thoughts..."
            rows={4}
            className="mt-2 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
          />
          <div className="mt-2 flex justify-end">
            <Button onClick={handleReply} loading={replying} disabled={!replyContent.trim()}>
              Post Reply
            </Button>
          </div>
        </Card>
      )}

      {thread?.isLocked && (
        <p className="mt-6 rounded-lg border border-border bg-surface-2 px-4 py-3 text-center text-sm text-fg-muted">
          This thread is locked. New replies are disabled.
        </p>
      )}
    </PageShell>
  );
}
