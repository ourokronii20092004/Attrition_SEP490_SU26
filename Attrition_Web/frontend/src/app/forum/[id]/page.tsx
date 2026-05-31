"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, ThumbsUp, ThumbsDown, Flag, Lock, Reply, ImagePlus } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { useAuth, useToast } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { SkeletonList, Skeleton } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { PostContent } from "@/components/post-content";
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

// Posts fetched in one page (max clamp) so the whole tree builds client-side. A thread beyond
// this many replies would need cursor paging — acceptable for current scale; revisit if needed.
const POST_FETCH_SIZE = 100;

export default function ThreadPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState("");

  const { data: thread } = useQuery({
    queryKey: qk.forum.thread(params.id),
    enabled: !!params.id,
    queryFn: async () => {
      const res = await forumApi.getThread(params.id);
      return res.success ? res.data : null;
    },
  });

  const postsKey = qk.forum.postsPage(params.id, 1);
  const { data: posts, isPending } = useQuery({
    queryKey: postsKey,
    enabled: !!params.id,
    queryFn: async () => {
      const res = await forumApi.getPosts(params.id, { page: 1, pageSize: POST_FETCH_SIZE });
      return res.success ? res.data : null;
    },
  });

  const tree = posts ? buildTree(posts.items) : [];

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
        <div className="mt-6 space-y-3">
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
  const [images, setImages] = useState<string[]>([]);
  const [uploading, setUploading] = useState(false);

  const submit = () => {
    if (value.trim() || images.length) { onSubmit(value.trim(), images); setValue(""); setImages([]); }
  };

  const pickImage = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      const res = await assetsApi.uploadInlineImage(file);
      if (res.success && res.data) setImages((prev) => [...prev, res.data]);
      else toast("Image upload failed.", "error");
    } catch {
      toast("Image upload failed.", "error");
    } finally {
      setUploading(false);
    }
  };

  return (
    <div>
      {label && <label className="text-sm font-medium text-fg">{label}</label>}
      <textarea
        value={value}
        autoFocus={autoFocus}
        onChange={(e) => setValue(e.target.value)}
        placeholder={placeholder ?? "Reply…"}
        rows={3}
        className="mt-2 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
      />
      {images.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-2">
          {images.map((url) => (
            <div key={url} className="relative">
              <img src={resolveMediaUrl(url) ?? ""} alt="" className="h-16 w-16 rounded object-cover" />
              <button
                onClick={() => setImages((prev) => prev.filter((u) => u !== url))}
                className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-danger text-xs text-white"
                aria-label="Remove image"
              >×</button>
            </div>
          ))}
        </div>
      )}
      <div className="mt-2 flex items-center justify-between">
        <label className="inline-flex cursor-pointer items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-fg">
          <ImagePlus size={14} /> {uploading ? "Uploading…" : "Add image"}
          <input type="file" accept="image/*" className="hidden" disabled={uploading}
            onChange={(e) => { pickImage(e.target.files?.[0]); e.target.value = ""; }} />
        </label>
        <Button size="sm" onClick={submit} loading={loading} disabled={uploading || (!value.trim() && !images.length)}>Reply</Button>
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
      <Card className="p-4">
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
            <PostContent content={node.content} className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-fg" />

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


