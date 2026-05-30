"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ThumbsUp, ThumbsDown, Flag } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { useAuth } from "@/lib/providers";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageLoader } from "@/components/ui/spinner";
import { Button } from "@/components/ui/button";
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
  const totalPages = posts ? Math.ceil(posts.totalCount / posts.pageSize) : 0;

  useEffect(() => {
    if (!params.id) return;
    forumApi.getThread(params.id).then((res) => {
      if (res.success) setThread(res.data);
    });
  }, [params.id]);

  useEffect(() => {
    if (!params.id) return;
    setLoading(true);
    forumApi
      .getPosts(params.id, { page, pageSize: 20 })
      .then((res) => {
        if (res.success) setPosts(res.data);
      })
      .finally(() => setLoading(false));
  }, [params.id, page]);

  const handleReply = async () => {
    if (!replyContent.trim() || !params.id) return;
    setReplying(true);
    try {
      await forumApi.createPost(params.id, { content: replyContent });
      setReplyContent("");
      const res = await forumApi.getPosts(params.id, { page, pageSize: 20 });
      if (res.success) setPosts(res.data);
    } catch {}
    setReplying(false);
  };

  const handleReact = async (postId: string, type: "like" | "dislike") => {
    try {
      await forumApi.react(postId, { reactionType: type });
      const res = await forumApi.getPosts(params.id, { page, pageSize: 20 });
      if (res.success) setPosts(res.data);
    } catch {}
  };

  if (loading && !thread) return <PageLoader />;

  return (
    <div className="mx-auto max-w-4xl px-4 py-8">
      <Link href="/forum" className="text-sm text-accent hover:underline">&larr; Forum</Link>

      {thread && (
        <div className="mt-4">
          <h1 className="font-display text-3xl font-bold text-fg">{thread.title}</h1>
          <p className="mt-1 text-sm text-fg-muted">
            {thread.categorySlug} &middot; by {thread.authorName} &middot; {thread.replyCount} replies
          </p>
        </div>
      )}

      {loading ? (
        <PageLoader />
      ) : (
        <div className="mt-6 space-y-4">
          {posts?.items.map((post) => (
            <div key={post.id} className="card p-4">
              <div className="flex items-start gap-3">
                <div className="shrink-0">
                  {post.authorAvatar ? (
                    <img src={resolveMediaUrl(post.authorAvatar) ?? ""} alt="" className="h-10 w-10 rounded-full object-cover" />
                  ) : (
                    <div className="flex h-10 w-10 items-center justify-center rounded-full bg-surface-3 text-sm font-medium text-fg-muted">
                      {post.authorName[0]}
                    </div>
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <Link href={`/u/${post.authorName}`} className="text-sm font-medium text-fg hover:text-accent">
                      {post.authorName}
                    </Link>
                    {post.authorRole === "Admin" && <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs text-accent">Admin</span>}
                    <span className="text-xs text-fg-subtle">{new Date(post.createdAt).toLocaleString()}</span>
                  </div>
                  <div className="mt-2 text-sm text-fg whitespace-pre-wrap">{post.content}</div>

                  <div className="mt-3 flex items-center gap-4">
                    <button
                      onClick={() => handleReact(post.id, "like")}
                      className={`flex items-center gap-1 text-xs ${post.currentUserReaction === "like" ? "text-accent" : "text-fg-subtle hover:text-fg"}`}
                    >
                      <ThumbsUp size={14} /> {post.likeCount}
                    </button>
                    <button
                      onClick={() => handleReact(post.id, "dislike")}
                      className={`flex items-center gap-1 text-xs ${post.currentUserReaction === "dislike" ? "text-danger" : "text-fg-subtle hover:text-fg"}`}
                    >
                      <ThumbsDown size={14} /> {post.dislikeCount}
                    </button>
                    {user && (
                      <button className="flex items-center gap-1 text-xs text-fg-subtle hover:text-warning">
                        <Flag size={14} /> Report
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-2">
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
          <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
          <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
        </div>
      )}

      {user && thread && !thread.isLocked && (
        <div className="mt-6">
          <textarea
            value={replyContent}
            onChange={(e) => setReplyContent(e.target.value)}
            placeholder="Write a reply..."
            rows={4}
            className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
          />
          <Button onClick={handleReply} loading={replying} disabled={!replyContent.trim()} className="mt-2">
            Post Reply
          </Button>
        </div>
      )}
    </div>
  );
}
