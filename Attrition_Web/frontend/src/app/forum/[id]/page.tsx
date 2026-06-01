"use client";

import { useEffect, useRef, useState } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, ThumbsUp, ThumbsDown, Flag, Lock, Reply, ImagePlus, Eye, Pencil } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { useAuth, useToast } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { SkeletonList, Skeleton } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { MarkdownContent } from "@/components/post-content";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import type { ForumPostDto } from "@/lib/types";

type PostNode = ForumPostDto & { children: PostNode[] };

/** Build a reply tree from the flat, chronological post list. Orphans (parent missing/removed)
 * fall back to top-level so nothing is hidden. */
function buildTree(posts: ForumPostDto[]): PostNode[] {
  const byId = new Map<string, PostNode>();
  for (const p of posts) byId.set(p.id, { ...p, children: [] });
  const roots: PostNode[] = [];
  for (const node of byId.values()) {
    const parent = node.parentPostId ? byId.get(node.parentPostId) : null;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }
  return roots;
}

/** The original post (thread starter): the top-level post (no parent) with the earliest
 * createdAt. Returns null if none of the loaded posts is top-level. */
function findOriginalPost(posts: ForumPostDto[]): ForumPostDto | null {
  let op: ForumPostDto | null = null;
  for (const p of posts) {
    if (p.parentPostId != null) continue;
    if (!op || new Date(p.createdAt).getTime() < new Date(op.createdAt).getTime()) op = p;
  }
  return op;
}

// First reply page size. Beyond this, a "Load more replies" button grows the pool and the tree
// rebuilds incrementally (orphans fall back to top-level, so partial loads stay coherent).
const REPLY_PAGE_SIZE = 50;

export default function ThreadPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState("");
  // Reply window grows by REPLY_PAGE_SIZE on "load more"; the key carries it so React Query
  // refetches the larger window and the whole set stays consistent (tree rebuilds cleanly).
  const [limit, setLimit] = useState(REPLY_PAGE_SIZE);

  const { data: thread } = useQuery({
    queryKey: qk.forum.thread(params.id),
    enabled: !!params.id,
    queryFn: async () => {
      const res = await forumApi.getThread(params.id);
      return res.success ? res.data : null;
    },
  });

  const postsKey = [...qk.forum.posts(params.id), "w", limit] as const;
  const { data: posts, isPending } = useQuery({
    queryKey: postsKey,
    enabled: !!params.id,
    queryFn: async () => {
      const res = await forumApi.getPosts(params.id, { page: 1, pageSize: limit });
      return res.success ? res.data : null;
    },
  });

  const allPosts = posts?.items ?? [];
  const originalPost = findOriginalPost(allPosts);
  // Replies = everything except the original post; build the nested tree from those.
  const tree = buildTree(allPosts.filter((p) => p.id !== originalPost?.id));
  const totalReplies = posts ? posts.totalCount - (originalPost ? 1 : 0) : 0;
  const loadedReplies = allPosts.length - (originalPost ? 1 : 0);
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
      await forumApi.createPost(params.id, { content, parentPostId, attachments });
    },
    onSuccess: invalidatePosts,
    onError: () => setActionError("Failed to post reply. Please try again."),
  });

  const reactMutation = useMutation({
    mutationFn: async ({ postId, type }: { postId: string; type: "like" | "dislike" }) => {
      await forumApi.react(postId, { reactionType: type });
    },
    // Optimistic: flip highlight + counts instantly, mirroring the backend toggle.
    onMutate: async ({ postId, type }) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const prev = queryClient.getQueryData<typeof posts>(postsKey);
      queryClient.setQueryData<typeof posts>(postsKey, (old) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((p) => {
            if (p.id !== postId) return p;
            let { likeCount, dislikeCount } = p;
            if (p.currentUserReaction === "like") likeCount--;
            if (p.currentUserReaction === "dislike") dislikeCount--;
            const next = p.currentUserReaction === type ? null : type;
            if (next === "like") likeCount++;
            if (next === "dislike") dislikeCount++;
            return { ...p, currentUserReaction: next, likeCount, dislikeCount };
          }),
        };
      });
      return { prev };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev) queryClient.setQueryData(postsKey, ctx.prev);
      setActionError("Failed to register your reaction. Please try again.");
    },
    onSettled: invalidatePosts,
  });

  const reportMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => {
      await forumApi.report(postId, { reason });
    },
    onSuccess: () => toast("Report submitted. Thank you.", "success"),
    onError: () => toast("Failed to submit report. Please try again.", "error"),
  });

  const handleReport = (postId: string) => {
    const reason = window.prompt("Why are you reporting this post?");
    if (!reason?.trim()) return;
    reportMutation.mutate({ postId, reason: reason.trim() });
  };

  if (isPending && !thread) {
    return (
      <PageShell size="lg">
        <Skeleton className="h-4 w-16" />
        <Skeleton className="mt-4 h-9 w-2/3" />
        <SkeletonList rows={4} className="mt-6" />
      </PageShell>
    );
  }

  const canReply = !!user && !!thread && !thread.isLocked;

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

      {isPending ? (
        <SkeletonList rows={4} className="mt-6" />
      ) : (
        <>
          {/* Original post: rendered as a distinct header block (markdown), not a reply (UIBD-1). */}
          {originalPost && (
            <Card id={`post-${originalPost.id}`} className="mt-6 p-5 transition-shadow">
              <div className="flex items-start gap-3">
                <Avatar src={originalPost.authorAvatar} name={originalPost.authorName} size="md" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link href={`/u/${encodeURIComponent(originalPost.authorName)}`} className="text-sm font-medium text-fg transition-colors hover:text-accent">
                      {originalPost.authorName}
                    </Link>
                    {originalPost.authorRole === "Admin" && (
                      <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>
                    )}
                    <span className="text-[10px] uppercase tracking-wider text-fg-subtle">Original post</span>
                    <span className="text-xs text-fg-subtle"><RelativeTime iso={originalPost.createdAt} /></span>
                  </div>
                  <MarkdownContent content={originalPost.content} className="prose-content mt-3 text-sm" />
                  {originalPost.attachments.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {originalPost.attachments.map((url) => (
                        <a key={url} href={resolveMediaUrl(url) ?? ""} target="_blank" rel="noopener noreferrer">
                          <img src={resolveMediaUrl(url) ?? ""} alt="" className="max-h-64 rounded-lg border border-border object-cover" />
                        </a>
                      ))}
                    </div>
                  )}
                  <div className="mt-3 flex items-center gap-1">
                    <button onClick={() => { setActionError(""); reactMutation.mutate({ postId: originalPost.id, type: "like" }); }}
                      className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${originalPost.currentUserReaction === "like" ? "bg-accent-soft text-accent" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                      <ThumbsUp size={14} /> {originalPost.likeCount}
                    </button>
                    <button onClick={() => { setActionError(""); reactMutation.mutate({ postId: originalPost.id, type: "dislike" }); }}
                      className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${originalPost.currentUserReaction === "dislike" ? "bg-danger/10 text-danger" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                      <ThumbsDown size={14} /> {originalPost.dislikeCount}
                    </button>
                    {!!user && (
                      <button onClick={() => handleReport(originalPost.id)}
                        className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-warning">
                        <Flag size={14} /> Report
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </Card>
          )}

          <div className="mt-4 space-y-3">
            {tree.map((node) => (
              <PostNodeView
                key={node.id}
                node={node}
                canReply={canReply}
                showReport={!!user}
                onReact={(postId, type) => { setActionError(""); reactMutation.mutate({ postId, type }); }}
                onReport={handleReport}
                onReply={(content, parentPostId, attachments) => replyMutation.mutate({ content, parentPostId, attachments })}
                replying={replyMutation.isPending}
              />
            ))}
            {tree.length === 0 && <p className="py-8 text-center text-fg-muted">No replies yet. Be the first.</p>}
          </div>

          {hasMore && (
            <div className="mt-4 flex justify-center">
              <Button variant="secondary" size="sm" onClick={() => setLimit((n) => n + REPLY_PAGE_SIZE)}>
                Load more replies ({totalReplies - loadedReplies} left)
              </Button>
            </div>
          )}
        </>
      )}

      {actionError && <p className="mt-4 text-sm text-danger">{actionError}</p>}

      {canReply && (
        <Card className="mt-6 p-4">
          <ReplyBox
            label="Write a reply"
            placeholder="Share your thoughts..."
            loading={replyMutation.isPending}
            onSubmit={(content, attachments) => replyMutation.mutate({ content, parentPostId: null, attachments })}
          />
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

function PostNodeView({ node, canReply, showReport, onReact, onReport, onReply, replying }: {
  node: PostNode; canReply: boolean; showReport: boolean;
  onReact: (postId: string, type: "like" | "dislike") => void;
  onReport: (postId: string) => void;
  onReply: (content: string, parentPostId: string, attachments: string[]) => void;
  replying: boolean;
}) {
  const [replyOpen, setReplyOpen] = useState(false);
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
        <div className="mt-3 space-y-3 border-l border-border pl-3 sm:pl-5">
          {node.children.map((child) => (
            <PostNodeView
              key={child.id}
              node={child}
              canReply={canReply}
              showReport={showReport}
              onReact={onReact}
              onReport={onReport}
              onReply={onReply}
              replying={replying}
            />
          ))}
        </div>
      )}
    </div>
  );
}


