"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ThumbsUp, ThumbsDown, Reply, MessageSquare, ImagePlus, Eye, Pencil } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAuth, useToast } from "@/lib/providers";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { MarkdownContent } from "@/components/post-content";
import { qk } from "@/lib/query-keys";
import type { ForumPostDto } from "@/lib/types";

type PostNode = ForumPostDto & { children: PostNode[] };

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

const PAGE_SIZE = 50;

export function WikiComments({ articleId, articleTitle }: { articleId: string; articleTitle: string }) {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [limit, setLimit] = useState(PAGE_SIZE);

  // Resolve (creating on first view) the article's comment thread.
  const { data: thread } = useQuery({
    queryKey: qk.wiki.commentThread(articleId),
    enabled: !!articleId,
    queryFn: async () => {
      const res = await forumApi.getWikiThread(articleId, articleTitle);
      return res.success ? res.data : null;
    },
  });

  const threadId = thread?.id;
  const postsKey = ["wiki", "comments", threadId, limit] as const;
  const { data: posts, isPending } = useQuery({
    queryKey: postsKey,
    enabled: !!threadId,
    queryFn: async () => {
      const res = await forumApi.getPosts(threadId!, { page: 1, pageSize: limit });
      return res.success ? res.data : null;
    },
  });

  const allPosts = posts?.items ?? [];
  const tree = buildTree(allPosts);
  const total = posts?.totalCount ?? 0;
  const hasMore = allPosts.length < total;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["wiki", "comments", threadId] });

  const replyMutation = useMutation({
    mutationFn: async ({ content, parentPostId }: { content: string; parentPostId: string | null }) => {
      await forumApi.createPost(threadId!, { content, parentPostId, attachments: [] });
    },
    onSuccess: invalidate,
    onError: () => toast("Failed to post comment. Please try again.", "error"),
  });

  const reactMutation = useMutation({
    mutationFn: async ({ postId, type }: { postId: string; type: "like" | "dislike" }) => {
      await forumApi.react(postId, { reactionType: type });
    },
    onSettled: invalidate,
  });

  const reportMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => {
      await forumApi.report(postId, { reason });
    },
    onSuccess: () => toast("Report submitted. Thank you.", "success"),
    onError: () => toast("Failed to submit report.", "error"),
  });

  const handleReport = (postId: string) => {
    const reason = window.prompt("Why are you reporting this comment?");
    if (reason?.trim()) reportMutation.mutate({ postId, reason: reason.trim() });
  };

  return (
    <section className="mt-10 border-t border-border pt-6">
      <h2 className="flex items-center gap-2 font-display text-xl font-bold text-fg">
        <MessageSquare size={18} /> Comments {total > 0 && <span className="text-sm font-normal text-fg-muted">({total})</span>}
      </h2>

      {user ? (
        <Card className="mt-4 p-4">
          <CommentBox loading={replyMutation.isPending} onSubmit={(content) => replyMutation.mutate({ content, parentPostId: null })} />
        </Card>
      ) : (
        <p className="mt-4 text-sm text-fg-muted">
          <Link href="/login" className="text-accent hover:underline">Sign in</Link> to join the discussion.
        </p>
      )}

      {isPending && threadId ? (
        <SkeletonList rows={3} className="mt-4" />
      ) : (
        <div className="mt-4 space-y-3">
          {tree.map((node) => (
            <CommentNode
              key={node.id}
              node={node}
              canReply={!!user}
              showReport={!!user}
              onReact={(postId, type) => reactMutation.mutate({ postId, type })}
              onReport={handleReport}
              onReply={(content, parentPostId) => replyMutation.mutate({ content, parentPostId })}
              replying={replyMutation.isPending}
            />
          ))}
          {tree.length === 0 && <p className="py-6 text-center text-sm text-fg-muted">No comments yet. Be the first.</p>}
        </div>
      )}

      {hasMore && (
        <div className="mt-4 flex justify-center">
          <Button variant="secondary" size="sm" onClick={() => setLimit((n) => n + PAGE_SIZE)}>
            Load more comments ({total - allPosts.length} left)
          </Button>
        </div>
      )}
    </section>
  );
}

function CommentBox({ loading, onSubmit, autoFocus, placeholder }: {
  loading: boolean; onSubmit: (content: string) => void; autoFocus?: boolean; placeholder?: string;
}) {
  const { toast } = useToast();
  const [value, setValue] = useState("");
  const [preview, setPreview] = useState(false);
  const [uploading, setUploading] = useState(false);

  const submit = () => { if (value.trim()) { onSubmit(value.trim()); setValue(""); setPreview(false); } };

  // Inline image upload → markdown token at the end (same flow as the forum reply box, QOLF-1).
  const addImage = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      const res = await assetsApi.uploadInlineImage(file);
      if (res.success && res.data) setValue((v) => `${v}\n![image](${res.data})\n`);
      else toast("Image upload failed.", "error");
    } catch { toast("Image upload failed.", "error"); }
    finally { setUploading(false); }
  };

  return (
    <div>
      {preview ? (
        <div className="min-h-[4rem] rounded-lg border border-border bg-surface-2 px-3 py-2">
          {value.trim() ? <MarkdownContent content={value} className="prose-content text-sm" /> : <p className="text-sm text-fg-subtle">Nothing to preview.</p>}
        </div>
      ) : (
        <textarea
          value={value}
          autoFocus={autoFocus}
          onChange={(e) => setValue(e.target.value)}
          placeholder={placeholder ?? "Add a comment (Markdown supported)…"}
          rows={3}
          className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
        />
      )}
      <div className="mt-2 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <label className="inline-flex cursor-pointer items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-fg">
            <ImagePlus size={14} /> {uploading ? "Uploading…" : "Image"}
            <input type="file" accept="image/*" className="hidden" disabled={uploading}
              onChange={(e) => { addImage(e.target.files?.[0]); e.target.value = ""; }} />
          </label>
          <button type="button" onClick={() => setPreview((v) => !v)} className="inline-flex items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-fg">
            {preview ? <><Pencil size={14} /> Edit</> : <><Eye size={14} /> Preview</>}
          </button>
        </div>
        <Button size="sm" onClick={submit} loading={loading} disabled={uploading || !value.trim()}>Comment</Button>
      </div>
    </div>
  );
}

function CommentNode({ node, canReply, showReport, onReact, onReport, onReply, replying }: {
  node: PostNode; canReply: boolean; showReport: boolean;
  onReact: (postId: string, type: "like" | "dislike") => void;
  onReport: (postId: string) => void;
  onReply: (content: string, parentPostId: string) => void;
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
              {node.authorRole === "Admin" && <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>}
              <span className="text-xs text-fg-subtle"><RelativeTime iso={node.createdAt} /></span>
            </div>
            <MarkdownContent content={node.content} className="prose-content mt-2 text-sm" />
            <div className="mt-2 flex items-center gap-1">
              <button onClick={() => onReact(node.id, "like")}
                className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${node.currentUserReaction === "like" ? "bg-accent-soft text-accent" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                <ThumbsUp size={14} /> {node.likeCount}
              </button>
              <button onClick={() => onReact(node.id, "dislike")}
                className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors ${node.currentUserReaction === "dislike" ? "bg-danger/10 text-danger" : "text-fg-subtle hover:bg-surface-2 hover:text-fg"}`}>
                <ThumbsDown size={14} /> {node.dislikeCount}
              </button>
              {canReply && (
                <button onClick={() => setReplyOpen((v) => !v)} className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg">
                  <Reply size={14} /> Reply
                </button>
              )}
              {showReport && (
                <button onClick={() => onReport(node.id)} className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs text-fg-subtle transition-colors hover:bg-surface-2 hover:text-warning">
                  Report
                </button>
              )}
            </div>
            {replyOpen && (
              <div className="mt-3">
                <CommentBox loading={replying} autoFocus placeholder={`Reply to @${node.authorName}…`}
                  onSubmit={(content) => { onReply(content, node.id); setReplyOpen(false); }} />
              </div>
            )}
          </div>
        </div>
      </Card>
      {node.children.length > 0 && (
        <div className="mt-3 space-y-3 border-l border-border pl-3 sm:pl-5">
          {node.children.map((child) => (
            <CommentNode key={child.id} node={child} canReply={canReply} showReport={showReport}
              onReact={onReact} onReport={onReport} onReply={onReply} replying={replying} />
          ))}
        </div>
      )}
    </div>
  );
}
