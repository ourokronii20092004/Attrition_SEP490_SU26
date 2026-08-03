"use client";

import { useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { clsx } from "clsx";
import { ArrowLeft, ThumbsUp, ThumbsDown, Flag, Lock, Reply, ImagePlus, Eye, Pencil, MessageSquare, Trash2 } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { useAuth, useToast, useConfirm } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { SkeletonList, Skeleton } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { MarkdownContent } from "@/components/post-content";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { makeOptimisticPost, addPostToPage, replacePostInPage, removePostFromPage } from "@/lib/forum-cache";
import { buildTree, indentsChildren, type PostNode } from "@/lib/forum-tree";
import type { ForumPostDto, ForumThreadDto, PaginatedResponse } from "@/lib/types";

// First reply page size. Beyond this, a "Load more replies" button grows the pool and the tree
// rebuilds incrementally (orphans fall back to top-level, so partial loads stay coherent).
const REPLY_PAGE_SIZE = 50;

export default function ThreadPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState("");
  // Reply window grows by REPLY_PAGE_SIZE on "load more"; the key carries it so React Query
  // refetches the larger window and the whole set stays consistent (tree rebuilds cleanly).
  const [limit, setLimit] = useState(REPLY_PAGE_SIZE);

  const { data: thread } = useQuery({
    queryKey: qk.forum.thread(params.id),
    enabled: !!params.id,
    // Keep briefly fresh so a just-created thread (seeded into cache on redirect) isn't instantly
    // refetched into a blank "ghost" if the read side lags behind the write.
    staleTime: 30_000,
    queryFn: async () => {
      const res = await forumApi.getThread(params.id);
      return res.success ? res.data : null;
    },
  });

  const postsKey = qk.forum.postsWindow(params.id, limit);
  const { data: posts, isPending } = useQuery({
    queryKey: postsKey,
    enabled: !!params.id,
    staleTime: 30_000,
    queryFn: async () => {
      const res = await forumApi.getPosts(params.id, { page: 1, pageSize: limit });
      return res.success ? res.data : null;
    },
  });

  const allPosts = posts?.items ?? [];
  const originalPost: ForumPostDto | null = thread ? {
    id: thread.id, threadId: thread.id, parentPostId: null, depth: 0,
    authorId: thread.authorId, authorName: thread.authorName, authorAvatar: thread.authorAvatar,
    authorRole: thread.authorRole, content: thread.content, attachments: thread.attachments,
    createdAt: thread.createdAt, updatedAt: thread.updatedAt, likeCount: thread.likeCount,
    dislikeCount: thread.dislikeCount, currentUserReaction: thread.currentUserReaction,
  } : null;
  const tree = buildTree(allPosts);
  const totalReplies = posts?.totalCount ?? 0;
  const loadedReplies = allPosts.length;
  const hasMore = loadedReplies < totalReplies;

  // Scroll to + briefly highlight a post when arriving via a notification deep-link (#post-id).
  useEffect(() => {
    if (isPending || typeof window === "undefined") return;
    const hash = window.location.hash;
    if (!hash.startsWith("#post-")) return;
    const el = document.getElementById(hash.slice(1));
    if (el) {
      el.scrollIntoView({ behavior: "smooth", block: "center" });
      el.classList.add("ring-2", "ring-accent");
      const t = setTimeout(() => el.classList.remove("ring-2", "ring-accent"), 2000);
      return () => clearTimeout(t);
    }
  }, [isPending, allPosts.length]);

  const invalidatePosts = () => queryClient.invalidateQueries({ queryKey: qk.forum.posts(params.id) });

  // Per-post reply: parentPostId null = top-level reply to the thread.
  const replyMutation = useMutation({
    mutationFn: async ({ content, parentPostId, attachments }: { content: string; parentPostId: string | null; attachments: string[] }) => {
      const res = await forumApi.createPost(params.id, { content, parentPostId, attachments });
      return res.success ? res.data : null;
    },
    // Show the reply the instant it's submitted; swap in the server's real post on success (so it
    // keeps its true id for reactions), or roll it back if the request fails. No refetch on settle,
    // so a lagging read side can't make the just-posted reply vanish.
    onMutate: async ({ content, parentPostId, attachments }) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const prev = queryClient.getQueryData<PaginatedResponse<ForumPostDto>>(postsKey);
      const optimistic = makeOptimisticPost({ threadId: params.id, content, parentPostId, attachments, user });
      queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => addPostToPage(old, optimistic));
      return { prev, tempId: optimistic.id };
    },
    onSuccess: (realPost, _vars, ctx) => {
      if (realPost && ctx) {
        queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => replacePostInPage(old, ctx.tempId, realPost));
      }
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev) queryClient.setQueryData(postsKey, ctx.prev);
      setActionError("Failed to post reply. Please try again.");
    },
  });

  const reactMutation = useMutation({
    mutationFn: async ({ postId, type }: { postId: string; type: "like" | "dislike" }) => {
      await forumApi.react(postId, { reactionType: type });
    },
    // Optimistic: flip highlight + counts instantly, mirroring the backend toggle.
    onMutate: async ({ postId, type }) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const prev = queryClient.getQueryData<typeof posts>(postsKey);
      const prevThread = queryClient.getQueryData<ForumThreadDto | null>(qk.forum.thread(params.id));
      const react = <T extends { id: string; likeCount: number; dislikeCount: number; currentUserReaction: string | null }>(p: T) => {
        if (p.id !== postId) return p;
        let { likeCount, dislikeCount } = p;
        if (p.currentUserReaction === "like") likeCount--;
        if (p.currentUserReaction === "dislike") dislikeCount--;
        const next = p.currentUserReaction === type ? null : type;
        if (next === "like") likeCount++;
        if (next === "dislike") dislikeCount++;
        return { ...p, currentUserReaction: next, likeCount, dislikeCount };
      };
      queryClient.setQueryData<ForumThreadDto | null>(qk.forum.thread(params.id), (old) => old ? react(old) : old);
      queryClient.setQueryData<typeof posts>(postsKey, (old) => {
        if (!old) return old;
        return { ...old, items: old.items.map(react) };
      });
      return { prev, prevThread };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev) queryClient.setQueryData(postsKey, ctx.prev);
      if (ctx?.prevThread) queryClient.setQueryData(qk.forum.thread(params.id), ctx.prevThread);
      setActionError("Failed to register your reaction. Please try again.");
    },
    onSettled: (_data, _error, variables) => {
      invalidatePosts();
      if (variables.postId === params.id) queryClient.invalidateQueries({ queryKey: qk.forum.thread(params.id) });
    },
  });

  const reportMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => {
      await forumApi.report(postId, { reason });
    },
    onSuccess: () => toast("Report submitted. Thank you.", "success"),
    onError: () => toast("Failed to submit report. Please try again.", "error"),
  });

  // Delete a post the current user owns. Optimistically drop it (children re-parent to top-level,
  // same as the server), roll back on failure.
  const deleteMutation = useMutation({
    mutationFn: async (postId: string) => {
      const res = await forumApi.deletePost(postId);
      if (!res.success) throw new Error(res.error ?? "Failed to delete");
    },
    onMutate: async (postId) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const prev = queryClient.getQueryData<PaginatedResponse<ForumPostDto>>(postsKey);
      queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => removePostFromPage(old, postId));
      return { prev };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev) queryClient.setQueryData(postsKey, ctx.prev);
      toast("Couldn't delete your post. Please try again.", "error");
    },
  });

  const handleReport = (postId: string) => {
    const reason = window.prompt("Why are you reporting this post?");
    if (!reason?.trim()) return;
    reportMutation.mutate({ postId, reason: reason.trim() });
  };

  const handleDelete = async (postId: string) => {
    const ok = await confirm({
      title: "Delete this reply?",
      message: "This permanently removes your reply. This can't be undone.",
      confirmLabel: "Delete",
      danger: true,
    });
    if (ok) { setActionError(""); deleteMutation.mutate(postId); }
  };

  // Deleting the root post deletes the whole discussion and all of its replies.
  const handleDeleteOriginalPost = async () => {
    if (!originalPost) return;
    const ok = await confirm({
      title: "Delete this discussion?",
      message: "This permanently removes the original post and all replies. This can't be undone.",
      confirmLabel: "Delete",
      danger: true,
    });
    if (!ok) return;
    try {
      const res = await forumApi.deletePost(originalPost.id);
      if (!res.success) throw new Error(res.error ?? "Failed to delete");
      toast("Your post was deleted.", "success");
      queryClient.invalidateQueries({ queryKey: qk.forum.threads() });
      queryClient.invalidateQueries({ queryKey: qk.forum.posts(params.id) });
      router.push("/forum");
    } catch {
      toast("Couldn't delete your post. Please try again.", "error");
    }
  };

  // Reacting needs an account. Rather than firing a doomed 401, send anonymous users to sign in
  // (returning them to this thread afterwards).
  const handleReact = (postId: string, type: "like" | "dislike") => {
    if (!user) { router.push(`/login?redirect=/forum/${params.id}`); return; }
    setActionError("");
    reactMutation.mutate({ postId, type });
  };

  if (isPending && !thread) {
    return (
      <PageShell size="lg">
        <Skeleton className="h-4 w-16" />
        <ThreadPostSkeleton />
      </PageShell>
    );
  }

  const canReply = !!user && !!thread && !thread.isLocked;

  return (
    <PageShell size="lg">
      <Link href="/forum" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Forum
      </Link>

      {isPending ? (
        <ThreadPostSkeleton />
      ) : (
        <>
          {/* The thread's opening post — a self-contained "post" card (title + body + actions all
              inside), styled like a social feed post rather than a comment. */}
          {originalPost && thread && (
            <ThreadPost
              post={originalPost}
              thread={thread}
              replyCount={totalReplies}
              canReport={!!user}
              canDelete={!!user && originalPost.authorId === user.id}
              onReact={(type) => handleReact(originalPost.id, type)}
              onReport={() => handleReport(originalPost.id)}
              onDelete={handleDeleteOriginalPost}
            />
          )}

          {actionError && <p className="mt-4 text-sm text-danger">{actionError}</p>}

          {/* Compose box sits directly beneath the post — not buried under every reply. */}
          {canReply ? (
            <Card className="mt-5 p-4">
              <ReplyBox
                label="Write a reply"
                placeholder="Share your thoughts…"
                loading={replyMutation.isPending}
                onSubmit={(content, attachments) => replyMutation.mutate({ content, parentPostId: null, attachments })}
              />
            </Card>
          ) : thread?.isLocked ? (
            <p className="mt-5 flex items-center justify-center gap-2 rounded-lg border border-border bg-surface-2 px-4 py-3 text-center text-sm text-fg-muted">
              <Lock size={14} /> This thread is locked. New replies are disabled.
            </p>
          ) : !user ? (
            <p className="mt-5 rounded-lg border border-border bg-surface-2 px-4 py-3 text-center text-sm text-fg-muted">
              <Link href={`/login?redirect=/forum/${params.id}`} className="font-medium text-accent hover:underline">Sign in</Link>{" "}
              to join the discussion.
            </p>
          ) : null}

          {/* Replies stay in the compact comment style, below the post. */}
          <div className="mt-8">
            <h2 className="font-display text-lg font-semibold tracking-tight text-fg">
              {totalReplies} {totalReplies === 1 ? "Reply" : "Replies"}
            </h2>
            <div className="mt-4 space-y-3">
              {tree.map((node) => (
                <PostNodeView
                  key={node.id}
                  node={node}
                  canReply={canReply}
                  showReport={!!user}
                  currentUserId={user?.id}
                  onReact={handleReact}
                  onReport={handleReport}
                  onReply={(content, parentPostId, attachments) => replyMutation.mutate({ content, parentPostId, attachments })}
                  onDelete={handleDelete}
                  replying={replyMutation.isPending}
                />
              ))}
              {tree.length === 0 && (
                <p className="rounded-lg border border-dashed border-border py-10 text-center text-sm text-fg-muted">
                  No replies yet. Be the first to respond.
                </p>
              )}
            </div>

            {hasMore && (
              <div className="mt-4 flex justify-center">
                <Button variant="secondary" size="sm" onClick={() => setLimit((n) => n + REPLY_PAGE_SIZE)}>
                  Load more replies ({totalReplies - loadedReplies} left)
                </Button>
              </div>
            )}
          </div>
        </>
      )}
    </PageShell>
  );
}

/**
 * The thread's opening post as a proper social-feed post card: byline + report on top, the thread
 * title and full-size markdown body in the middle, and an action bar (reactions + reply count) at
 * the bottom. Deliberately distinct from the compact reply cards below it.
 */
function ThreadPost({ post, thread, replyCount, canReport, canDelete, onReact, onReport, onDelete }: {
  post: ForumPostDto;
  thread: ForumThreadDto;
  replyCount: number;
  canReport: boolean;
  canDelete: boolean;
  onReact: (type: "like" | "dislike") => void;
  onReport: () => void;
  onDelete: () => void;
}) {
  return (
    <Card id={`post-${post.id}`} className="mt-6 p-5 transition-shadow sm:p-7">
      {/* Byline — author on the left, report tucked into the top-right corner. */}
      <div className="flex items-start gap-3 sm:gap-4">
        <Link href={`/u/${encodeURIComponent(post.authorName)}`} className="shrink-0">
          <Avatar src={post.authorAvatar} name={post.authorName} size="lg" />
        </Link>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <Link href={`/u/${encodeURIComponent(post.authorName)}`} className="font-semibold text-fg transition-colors hover:text-accent">
              {post.authorName}
            </Link>
            {post.authorRole === "Admin" && (
              <span className="rounded bg-accent-soft px-1.5 py-0.5 text-[11px] font-medium text-accent">Admin</span>
            )}
          </div>
          <div className="mt-0.5 flex flex-wrap items-center gap-x-1.5 text-xs text-fg-subtle">
            <RelativeTime iso={post.createdAt} />
            <span aria-hidden>·</span>
            <span className="rounded-full bg-surface-2 px-2 py-0.5 font-medium text-fg-muted">{thread.categorySlug}</span>
            {thread.isLocked && (
              <span className="inline-flex items-center gap-1 font-medium text-warning"><Lock size={11} /> Locked</span>
            )}
          </div>
        </div>
        {(canReport || canDelete) && (
          <div className="-mr-1.5 -mt-1.5 flex shrink-0 items-center gap-0.5">
            {canReport && (
              <button
                onClick={onReport}
                aria-label="Report post"
                className="rounded-lg p-2 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-warning"
              >
                <Flag size={16} />
              </button>
            )}
            {canDelete && (
              <button
                onClick={onDelete}
                aria-label="Delete post"
                className="rounded-lg p-2 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-danger"
              >
                <Trash2 size={16} />
              </button>
            )}
          </div>
        )}
      </div>

      {/* Title + body, both inside the post card. */}
      <h1 className="mt-4 break-words font-display text-2xl font-bold leading-tight tracking-tight text-balance text-fg sm:text-3xl">
        {thread.title}
      </h1>
      <MarkdownContent content={post.content} className="prose-content mt-3" />

      {post.attachments.length > 0 && (
        <div className="mt-4 flex flex-wrap gap-2">
          {post.attachments.map((url) => (
            <a key={url} href={resolveMediaUrl(url) ?? ""} target="_blank" rel="noopener noreferrer">
              <img src={resolveMediaUrl(url) ?? ""} alt="" className="max-h-80 rounded-lg border border-border object-cover" />
            </a>
          ))}
        </div>
      )}

      {/* Action bar — reactions live down here; reply count anchored to the right. */}
      <div className="mt-5 flex items-center gap-2 border-t border-border pt-4">
        <VoteButton active={post.currentUserReaction === "like"} tone="accent" icon={ThumbsUp} count={post.likeCount} onClick={() => onReact("like")} label="Like" />
        <VoteButton active={post.currentUserReaction === "dislike"} tone="danger" icon={ThumbsDown} count={post.dislikeCount} onClick={() => onReact("dislike")} label="Dislike" />
        <span className="ml-auto inline-flex items-center gap-1.5 text-sm font-medium text-fg-muted">
          <MessageSquare size={16} /> {replyCount}
        </span>
      </div>
    </Card>
  );
}

/** Pill-style reaction button for the post card's action bar. Neutral until it's the user's own
 * active vote, when it takes the accent (like) or danger (dislike) tint. */
function VoteButton({ active, tone, icon: Icon, count, onClick, label }: {
  active: boolean; tone: "accent" | "danger";
  icon: React.ComponentType<{ size?: number }>; count: number; onClick: () => void; label: string;
}) {
  return (
    <button
      onClick={onClick}
      aria-label={label}
      aria-pressed={active}
      className={clsx(
        "inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-sm font-medium transition-colors",
        active
          ? tone === "danger" ? "bg-danger/10 text-danger" : "bg-accent-soft text-accent"
          : "bg-surface-2 text-fg-muted hover:bg-surface-3 hover:text-fg",
      )}
    >
      <Icon size={16} /> <span className="tabular-nums">{count}</span>
    </button>
  );
}

function ThreadPostSkeleton() {
  return (
    <Card className="mt-6 p-5 sm:p-7">
      <div className="flex items-center gap-4">
        <Skeleton className="h-16 w-16 rounded-full" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-3 w-28" />
        </div>
      </div>
      <Skeleton className="mt-5 h-8 w-3/4" />
      <div className="mt-4 space-y-2.5">
        {[0, 1, 2, 3].map((i) => <Skeleton key={i} className={clsx("h-4", i === 3 ? "w-1/2" : "w-full")} />)}
      </div>
      <div className="mt-5 flex gap-2 border-t border-border pt-4">
        <Skeleton className="h-9 w-20 rounded-full" />
        <Skeleton className="h-9 w-20 rounded-full" />
      </div>
    </Card>
  );
}

function ReplyBox({ label, placeholder, loading, onSubmit, autoFocus }: {
  label?: string; placeholder?: string; loading: boolean;
  onSubmit: (content: string, attachments: string[]) => void; autoFocus?: boolean;
}) {
  const { toast } = useToast();
  const [value, setValue] = useState("");
  const [preview, setPreview] = useState(false);
  const [uploading, setUploading] = useState(false);
  const taRef = useRef<HTMLTextAreaElement | null>(null);

  const submit = () => {
    if (value.trim()) { onSubmit(value.trim(), []); setValue(""); setPreview(false); }
  };

  // Upload then insert a markdown image token at the cursor (mirrors the new-thread editor),
  // so forum replies use the same markdown+image flow as threads (QOLF-1).
  const insertImage = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      const res = await assetsApi.uploadInlineImage(file);
      if (res.success && res.data) {
        const md = `\n![${file.name}](${res.data})\n`;
        const ta = taRef.current;
        const at = ta ? ta.selectionStart : value.length;
        setValue((cur) => cur.slice(0, at) + md + cur.slice(at));
      } else {
        toast("Image upload failed.", "error");
      }
    } catch {
      toast("Image upload failed.", "error");
    } finally {
      setUploading(false);
    }
  };

  return (
    <div>
      {label && <label className="text-sm font-medium text-fg">{label}</label>}
      {preview ? (
        <div className="mt-2 min-h-[5rem] rounded-lg border border-border bg-surface-2 px-3 py-2">
          {value.trim()
            ? <MarkdownContent content={value} className="prose-content text-sm" />
            : <p className="text-sm text-fg-subtle">Nothing to preview yet.</p>}
        </div>
      ) : (
        <textarea
          value={value}
          autoFocus={autoFocus}
          ref={taRef}
          onChange={(e) => setValue(e.target.value)}
          placeholder={placeholder ?? "Reply in Markdown…"}
          rows={3}
          className="mt-2 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
        />
      )}
      <div className="mt-2 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <label className="inline-flex cursor-pointer items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-fg">
            <ImagePlus size={14} /> {uploading ? "Uploading…" : "Add image"}
            <input type="file" accept="image/*" className="hidden" disabled={uploading}
              onChange={(e) => { insertImage(e.target.files?.[0]); e.target.value = ""; }} />
          </label>
          <button type="button" onClick={() => setPreview((v) => !v)}
            className="inline-flex items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-fg">
            {preview ? <><Pencil size={14} /> Edit</> : <><Eye size={14} /> Preview</>}
          </button>
        </div>
        <Button size="sm" onClick={submit} loading={loading} disabled={uploading || !value.trim()}>Reply</Button>
      </div>
    </div>
  );
}

function PostNodeView({ node, level = 0, replyingToName, canReply, showReport, currentUserId, onReact, onReport, onReply, onDelete, replying }: {
  node: PostNode; level?: number; replyingToName?: string; canReply: boolean; showReport: boolean; currentUserId?: string;
  onReact: (postId: string, type: "like" | "dislike") => void;
  onReport: (postId: string) => void;
  onReply: (content: string, parentPostId: string, attachments: string[]) => void;
  onDelete: (postId: string) => void;
  replying: boolean;
}) {
  const [replyOpen, setReplyOpen] = useState(false);
  const canDelete = !!currentUserId && node.authorId === currentUserId;
  // Past the indent cap, deeper replies render flush instead of nesting further (see forum-tree).
  const indent = indentsChildren(level);
  return (
    <div>
      <Card id={`post-${node.id}`} className="p-4 transition-shadow">
        <div className="flex items-start gap-3">
          <Avatar src={node.authorAvatar} name={node.authorName} size="md" />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Link href={`/u/${encodeURIComponent(node.authorName)}`} className="text-sm font-medium text-fg transition-colors hover:text-accent">
                {node.authorName}
              </Link>
              {node.authorRole === "Admin" && (
                <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>
              )}
              <span className="text-xs text-fg-subtle"><RelativeTime iso={node.createdAt} /></span>
              {replyingToName && (
                <span className="inline-flex items-center gap-1 text-xs text-fg-subtle">
                  <Reply size={11} aria-hidden /> replying to
                  <span className="font-medium text-fg-muted">@{replyingToName}</span>
                </span>
              )}
            </div>
            <MarkdownContent content={node.content} className="prose-content mt-2 text-sm" />

            {node.attachments.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2">
                {node.attachments.map((url) => (
                  <a key={url} href={resolveMediaUrl(url) ?? ""} target="_blank" rel="noopener noreferrer">
                    <img src={resolveMediaUrl(url) ?? ""} alt="" className="max-h-48 rounded-lg border border-border object-cover" />
                  </a>
                ))}
              </div>
            )}

            <div className="mt-3 flex items-center gap-1">
              <button onClick={() => onReact(node.id, "like")}
                className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${node.currentUserReaction === "like" ? "bg-accent-soft text-accent" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                <ThumbsUp size={14} /> {node.likeCount}
              </button>
              <button onClick={() => onReact(node.id, "dislike")}
                className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${node.currentUserReaction === "dislike" ? "bg-danger/10 text-danger" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                <ThumbsDown size={14} /> {node.dislikeCount}
              </button>
              {canReply && (
                <button onClick={() => setReplyOpen((v) => !v)}
                  className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg">
                  <Reply size={14} /> Reply
                </button>
              )}
              {showReport && (
                <button onClick={() => onReport(node.id)}
                  className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-warning">
                  <Flag size={14} /> Report
                </button>
              )}
              {canDelete && (
                <button onClick={() => onDelete(node.id)}
                  className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-danger">
                  <Trash2 size={14} /> Delete
                </button>
              )}
            </div>

            {replyOpen && (
              <div className="mt-3">
                <ReplyBox
                  placeholder={`Reply to @${node.authorName}…`}
                  loading={replying}
                  autoFocus
                  onSubmit={(content, attachments) => { onReply(content, node.id, attachments); setReplyOpen(false); }}
                />
              </div>
            )}
          </div>
        </div>
      </Card>

      {node.children.length > 0 && (
        <div className={indent ? "mt-3 space-y-3 border-l border-border pl-3 sm:pl-5" : "mt-3 space-y-3"}>
          {node.children.map((child) => (
            <PostNodeView
              key={child.id}
              node={child}
              level={level + 1}
              // Once the indent stops, the visual nesting no longer says who is being answered,
              // so name the parent explicitly on those replies.
              replyingToName={indent ? undefined : node.authorName}
              canReply={canReply}
              showReport={showReport}
              currentUserId={currentUserId}
              onReact={onReact}
              onReport={onReport}
              onReply={onReply}
              onDelete={onDelete}
              replying={replying}
            />
          ))}
        </div>
      )}
    </div>
  );
}
