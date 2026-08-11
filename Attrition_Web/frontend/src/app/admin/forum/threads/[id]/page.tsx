"use client";

import { useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { clsx } from "clsx";
import { ArrowLeft, EyeOff, ImagePlus, Eye, Pencil, Lock, MessageSquare, Reply, ThumbsDown, ThumbsUp } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { MarkdownContent } from "@/components/post-content";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { makeOptimisticPost, addPostToPage, replacePostInPage, removePostFromPage } from "@/lib/forum-cache";
import { useAdminPageLabel } from "@/lib/hooks/use-admin-page-label";
import { buildTree, indentsChildren, type PostNode } from "@/lib/forum-tree";
import type { ForumPostDto, ForumThreadDto, PaginatedResponse } from "@/lib/types";
import { LIVE_FAST, liveWhenFocused } from "@/lib/live";

const REPLY_PAGE_SIZE = 50;

export default function AdminThreadPostsPage() {
  const { user } = useAuth();
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const [limit, setLimit] = useState(REPLY_PAGE_SIZE);
  const [actionError, setActionError] = useState("");
  const threadId = params.id;

  const { data: thread } = useQuery({
    queryKey: qk.forum.thread(threadId),
    enabled: user?.role === "Admin" && !!threadId,
    queryFn: async () => {
      const res = await forumApi.getThread(threadId);
      return res.success ? res.data : null;
    },
  });
  useAdminPageLabel(thread ? `Threads · ${thread.title}` : null);

  const postsKey = qk.forum.postsWindow(threadId, limit);
  const { data: posts, isPending } = useQuery({
    queryKey: postsKey,
    // Moderators watch active threads; new replies should land without a refresh.
    refetchInterval: liveWhenFocused(LIVE_FAST),
    enabled: user?.role === "Admin" && !!threadId,
    queryFn: async () => {
      const res = await forumApi.getPosts(threadId, { page: 1, pageSize: limit });
      return res.success ? res.data : null;
    },
  });

  const allPosts = posts?.items ?? [];
  const tree = buildTree(allPosts);
  const totalReplies = posts?.totalCount ?? 0;
  const hasMore = allPosts.length < totalReplies;
  const rootPost: ForumPostDto | null = thread ? {
    id: thread.id, threadId: thread.id, parentPostId: null, depth: 0,
    authorId: thread.authorId, authorName: thread.authorName, authorAvatar: thread.authorAvatar,
    authorRole: thread.authorRole, content: thread.content, attachments: thread.attachments,
    createdAt: thread.createdAt, updatedAt: thread.updatedAt, likeCount: thread.likeCount,
    dislikeCount: thread.dislikeCount, currentUserReaction: thread.currentUserReaction,
  } : null;

  useEffect(() => {
    if (isPending || typeof window === "undefined") return;
    const hash = window.location.hash;
    if (!hash.startsWith("#post-")) return;
    const element = document.getElementById(hash.slice(1));
    if (!element) return;
    element.scrollIntoView({ behavior: "smooth", block: "center" });
    element.classList.add("ring-2", "ring-accent");
    const timer = setTimeout(() => element.classList.remove("ring-2", "ring-accent"), 2000);
    return () => clearTimeout(timer);
  }, [isPending, allPosts.length]);

  const invalidatePosts = () => queryClient.invalidateQueries({ queryKey: qk.forum.posts(threadId) });

  const replyMutation = useMutation({
    mutationFn: async ({ content, parentPostId, attachments }: { content: string; parentPostId: string | null; attachments: string[] }) => {
      const res = await forumApi.createPost(threadId, { content, parentPostId, attachments });
      if (!res.success || !res.data) throw new Error(res.error ?? "Could not post reply");
      return res.data;
    },
    onMutate: async ({ content, parentPostId, attachments }) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const previous = queryClient.getQueryData<PaginatedResponse<ForumPostDto>>(postsKey);
      const optimistic = makeOptimisticPost({ threadId, content, parentPostId, attachments, user });
      queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => addPostToPage(old, optimistic));
      return { previous, tempId: optimistic.id };
    },
    onSuccess: (post, _variables, context) => {
      if (context) queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => replacePostInPage(old, context.tempId, post));
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(postsKey, context.previous);
      toast("Could not post the reply.", "error");
    },
  });

  const reactMutation = useMutation({
    mutationFn: async ({ postId, type }: { postId: string; type: "like" | "dislike" }) => forumApi.react(postId, { reactionType: type }),
    onMutate: async ({ postId, type }) => {
      await queryClient.cancelQueries({ queryKey: postsKey });
      const previous = queryClient.getQueryData<typeof posts>(postsKey);
      const previousThread = queryClient.getQueryData<ForumThreadDto | null>(qk.forum.thread(threadId));
      const react = <T extends { id: string; likeCount: number; dislikeCount: number; currentUserReaction: string | null }>(post: T) => {
        if (post.id !== postId) return post;
        let { likeCount, dislikeCount } = post;
        if (post.currentUserReaction === "like") likeCount--;
        if (post.currentUserReaction === "dislike") dislikeCount--;
        const next = post.currentUserReaction === type ? null : type;
        if (next === "like") likeCount++;
        if (next === "dislike") dislikeCount++;
        return { ...post, likeCount, dislikeCount, currentUserReaction: next };
      };
      queryClient.setQueryData<ForumThreadDto | null>(qk.forum.thread(threadId), (old) => old ? react(old) : old);
      queryClient.setQueryData<typeof posts>(postsKey, (old) => old ? { ...old, items: old.items.map(react) } : old);
      return { previous, previousThread };
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(postsKey, context.previous);
      if (context?.previousThread) queryClient.setQueryData(qk.forum.thread(threadId), context.previousThread);
      setActionError("Failed to register the reaction. Please try again.");
    },
    onSettled: (_data, _error, variables) => {
      invalidatePosts();
      if (variables.postId === threadId) queryClient.invalidateQueries({ queryKey: qk.forum.thread(threadId) });
    },
  });

  const removeMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => {
      const res = await forumApi.removePost(postId, { reason });
      if (!res.success) throw new Error(res.error ?? "Could not remove post");
      return postId;
    },
    onMutate: async ({ postId }) => {
      if (postId === threadId) return;
      await queryClient.cancelQueries({ queryKey: postsKey });
      const previous = queryClient.getQueryData<PaginatedResponse<ForumPostDto>>(postsKey);
      queryClient.setQueryData<PaginatedResponse<ForumPostDto>>(postsKey, (old) => removePostFromPage(old, postId));
      return { previous };
    },
    onSuccess: (postId) => {
      toast(postId === threadId ? "Discussion removed." : "Reply removed.", "success");
      queryClient.invalidateQueries({ queryKey: qk.admin.forum.threads() });
      queryClient.invalidateQueries({ queryKey: qk.forum.threads() });
      if (postId === threadId) router.push("/admin/forum/threads");
      else invalidatePosts();
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(postsKey, context.previous);
      toast("Could not remove the post.", "error");
    },
  });

  const removePost = async (postId: string) => {
    const reason = window.prompt("Reason for removing this post?");
    if (!reason?.trim()) return;
    const root = postId === threadId;
    const approved = await confirm({
      title: root ? "Remove this discussion?" : "Remove this reply?",
      message: root ? "This hides the discussion from users." : "This hides the reply from users.",
      danger: true,
      confirmLabel: "Remove",
    });
    if (approved) removeMutation.mutate({ postId, reason: reason.trim() });
  };

  if (!user || user.role !== "Admin") return null;
  if (isPending && !thread) return <div className="mx-auto max-w-4xl"><Skeleton className="h-4 w-20" /><ThreadPostSkeleton /></div>;
  const canReply = !!thread && !thread.isLocked;

  return <div className="mx-auto max-w-4xl">
    <Link href="/admin/forum/threads" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg"><ArrowLeft size={16} /> Threads</Link>
    {rootPost && thread && <ThreadPost post={rootPost} thread={thread} replyCount={totalReplies} removing={removeMutation.isPending && removeMutation.variables?.postId === rootPost.id} onReact={(type) => reactMutation.mutate({ postId: rootPost.id, type })} onRemove={() => removePost(rootPost.id)} />}
    {actionError && <p className="mt-4 text-sm text-danger">{actionError}</p>}

    {canReply ? <Card className="mt-5 p-4"><ReplyBox label="Reply as admin" placeholder="Write a reply…" loading={replyMutation.isPending} onSubmit={(content, attachments) => replyMutation.mutate({ content, parentPostId: null, attachments })} /></Card> : thread?.isLocked ? <p className="mt-5 flex items-center justify-center gap-2 rounded-lg border border-border bg-surface-2 px-4 py-3 text-sm text-fg-muted"><Lock size={14} /> This thread is locked.</p> : null}

    <div className="mt-8">
      <h2 className="font-display text-lg font-semibold tracking-tight text-fg">{totalReplies} {totalReplies === 1 ? "Reply" : "Replies"}</h2>
      <div className="mt-4 space-y-3">
        {tree.map((node) => <PostNodeView key={node.id} node={node} canReply={canReply} replying={replyMutation.isPending} removingId={removeMutation.variables?.postId} onReact={(postId, type) => reactMutation.mutate({ postId, type })} onReply={(content, parentPostId, attachments) => replyMutation.mutate({ content, parentPostId, attachments })} onRemove={removePost} />)}
        {!tree.length && <p className="rounded-lg border border-dashed border-border py-10 text-center text-sm text-fg-muted">No replies yet.</p>}
      </div>
      {hasMore && <div className="mt-4 flex justify-center"><Button variant="secondary" size="sm" onClick={() => setLimit((value) => value + REPLY_PAGE_SIZE)}>Load more replies ({totalReplies - allPosts.length} left)</Button></div>}
    </div>
  </div>;
}

function ThreadPost({ post, thread, replyCount, removing, onReact, onRemove }: { post: ForumPostDto; thread: ForumThreadDto; replyCount: number; removing: boolean; onReact: (type: "like" | "dislike") => void; onRemove: () => void }) {
  return <Card id={`post-${post.id}`} className="mt-6 p-5 transition-shadow sm:p-7">
    <div className="flex items-start gap-3 sm:gap-4"><Link href={`/admin/users/${post.authorId}`} className="shrink-0"><Avatar src={post.authorAvatar} name={post.authorName} size="lg" /></Link><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><Link href={`/admin/users/${post.authorId}`} className="font-semibold text-fg hover:text-accent">{post.authorName}</Link>{post.authorRole === "Admin" && <span className="rounded bg-accent-soft px-1.5 py-0.5 text-[11px] font-medium text-accent">Admin</span>}</div><div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-fg-subtle"><RelativeTime iso={post.createdAt} /><span aria-hidden>·</span><span className="rounded-full bg-surface-2 px-2 py-0.5 font-medium text-fg-muted">{thread.categorySlug}</span>{thread.isLocked && <span className="inline-flex items-center gap-1 font-medium text-warning"><Lock size={11} /> Locked</span>}</div></div><Button size="sm" variant="danger" loading={removing} onClick={onRemove}><EyeOff size={14} className="mr-1" /> Remove</Button></div>
    <h1 className="mt-4 break-words font-display text-2xl font-bold leading-tight tracking-tight text-fg sm:text-3xl">{thread.title}</h1><MarkdownContent content={post.content} className="prose-content mt-3" /><Attachments urls={post.attachments} large />
    <div className="mt-5 flex items-center gap-2 border-t border-border pt-4"><VoteButton active={post.currentUserReaction === "like"} tone="accent" icon={ThumbsUp} count={post.likeCount} onClick={() => onReact("like")} label="Like" /><VoteButton active={post.currentUserReaction === "dislike"} tone="danger" icon={ThumbsDown} count={post.dislikeCount} onClick={() => onReact("dislike")} label="Dislike" /><span className="ml-auto inline-flex items-center gap-1.5 text-sm font-medium text-fg-muted"><MessageSquare size={16} /> {replyCount}</span></div>
  </Card>;
}

function PostNodeView({ node, level = 0, replyingToName, canReply, replying, removingId, onReact, onReply, onRemove }: { node: PostNode; level?: number; replyingToName?: string; canReply: boolean; replying: boolean; removingId?: string; onReact: (postId: string, type: "like" | "dislike") => void; onReply: (content: string, parentPostId: string, attachments: string[]) => void; onRemove: (postId: string) => void }) {
  const [replyOpen, setReplyOpen] = useState(false);
  // Past the indent cap, deeper replies render flush instead of nesting further (see forum-tree).
  const indent = indentsChildren(level);
  return <div><Card id={`post-${node.id}`} className="p-4 transition-shadow"><div className="flex items-start gap-3"><Avatar src={node.authorAvatar} name={node.authorName} size="md" /><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><Link href={`/admin/users/${node.authorId}`} className="text-sm font-medium text-fg hover:text-accent">{node.authorName}</Link>{node.authorRole === "Admin" && <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>}<span className="text-xs text-fg-subtle"><RelativeTime iso={node.createdAt} /></span>{replyingToName && <span className="inline-flex items-center gap-1 text-xs text-fg-subtle"><Reply size={11} aria-hidden /> replying to <span className="font-medium text-fg-muted">@{replyingToName}</span></span>}</div><MarkdownContent content={node.content} className="prose-content mt-2 text-sm" /><Attachments urls={node.attachments} />
    <div className="mt-3 flex flex-wrap items-center gap-1"><ReplyAction active={node.currentUserReaction === "like"} onClick={() => onReact(node.id, "like")}><ThumbsUp size={14} /> {node.likeCount}</ReplyAction><ReplyAction active={node.currentUserReaction === "dislike"} danger onClick={() => onReact(node.id, "dislike")}><ThumbsDown size={14} /> {node.dislikeCount}</ReplyAction>{canReply && <ReplyAction onClick={() => setReplyOpen((value) => !value)}><Reply size={14} /> Reply</ReplyAction>}<Button size="sm" variant="danger" loading={removingId === node.id} onClick={() => onRemove(node.id)}><EyeOff size={14} className="mr-1" /> Remove</Button></div>
    {replyOpen && <div className="mt-3"><ReplyBox placeholder={`Reply to @${node.authorName}…`} loading={replying} autoFocus onSubmit={(content, attachments) => { onReply(content, node.id, attachments); setReplyOpen(false); }} /></div>}</div></div></Card>
    {!!node.children.length && <div className={indent ? "mt-3 space-y-3 border-l border-border pl-3 sm:pl-5" : "mt-3 space-y-3"}>{node.children.map((child) => <PostNodeView key={child.id} node={child} level={level + 1} replyingToName={indent ? undefined : node.authorName} canReply={canReply} replying={replying} removingId={removingId} onReact={onReact} onReply={onReply} onRemove={onRemove} />)}</div>}
  </div>;
}

function ReplyBox({ label, placeholder, loading, onSubmit, autoFocus }: { label?: string; placeholder?: string; loading: boolean; onSubmit: (content: string, attachments: string[]) => void; autoFocus?: boolean }) {
  const { toast } = useToast();
  const [value, setValue] = useState("");
  const [preview, setPreview] = useState(false);
  const [uploading, setUploading] = useState(false);
  const textarea = useRef<HTMLTextAreaElement | null>(null);
  const submit = () => { if (value.trim()) { onSubmit(value.trim(), []); setValue(""); setPreview(false); } };
  const insertImage = async (file?: File) => {
    if (!file) return;
    setUploading(true);
    try {
      const res = await assetsApi.uploadInlineImage(file);
      if (!res.success || !res.data) throw new Error();
      const token = `\n![${file.name}](${res.data})\n`;
      const at = textarea.current?.selectionStart ?? value.length;
      setValue((current) => current.slice(0, at) + token + current.slice(at));
    } catch { toast("Image upload failed.", "error"); }
    finally { setUploading(false); }
  };
  return <div>{label && <label className="text-sm font-medium text-fg">{label}</label>}{preview ? <div className="mt-2 min-h-20 rounded-lg border border-border bg-surface-2 px-3 py-2">{value.trim() ? <MarkdownContent content={value} className="prose-content text-sm" /> : <p className="text-sm text-fg-subtle">Nothing to preview yet.</p>}</div> : <textarea ref={textarea} value={value} autoFocus={autoFocus} onChange={(event) => setValue(event.target.value)} placeholder={placeholder ?? "Reply in Markdown…"} rows={3} className="mt-2 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent" />}<div className="mt-2 flex items-center justify-between"><div className="flex items-center gap-2"><label className="inline-flex cursor-pointer items-center gap-1.5 text-xs text-fg-subtle hover:text-fg"><ImagePlus size={14} /> {uploading ? "Uploading…" : "Add image"}<input type="file" accept="image/*" className="hidden" disabled={uploading} onChange={(event) => { insertImage(event.target.files?.[0]); event.target.value = ""; }} /></label><button type="button" onClick={() => setPreview((value) => !value)} className="inline-flex items-center gap-1.5 text-xs text-fg-subtle hover:text-fg">{preview ? <><Pencil size={14} /> Edit</> : <><Eye size={14} /> Preview</>}</button></div><Button size="sm" onClick={submit} loading={loading} disabled={uploading || !value.trim()}>Reply</Button></div></div>;
}

function Attachments({ urls, large = false }: { urls: string[]; large?: boolean }) {
  if (!urls.length) return null;
  return <div className="mt-4 flex flex-wrap gap-2">{urls.map((url) => <a key={url} href={resolveMediaUrl(url) ?? ""} target="_blank" rel="noopener noreferrer"><img src={resolveMediaUrl(url) ?? ""} alt="" className={clsx("rounded-lg border border-border object-cover", large ? "max-h-80" : "max-h-48")} /></a>)}</div>;
}

function VoteButton({ active, tone, icon: Icon, count, onClick, label }: { active: boolean; tone: "accent" | "danger"; icon: React.ComponentType<{ size?: number }>; count: number; onClick: () => void; label: string }) {
  return <button onClick={onClick} aria-label={label} aria-pressed={active} className={clsx("inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-sm font-medium transition-colors", active ? tone === "danger" ? "bg-danger/10 text-danger" : "bg-accent-soft text-accent" : "bg-surface-2 text-fg-muted hover:bg-surface-3 hover:text-fg")}><Icon size={16} /><span className="tabular-nums">{count}</span></button>;
}

function ReplyAction({ active, danger, onClick, children }: { active?: boolean; danger?: boolean; onClick: () => void; children: React.ReactNode }) {
  return <button onClick={onClick} className={clsx("inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs transition-colors", active ? danger ? "bg-danger/10 text-danger" : "bg-accent-soft text-accent" : "text-fg-subtle hover:bg-surface-2 hover:text-fg")}>{children}</button>;
}

function ThreadPostSkeleton() {
  return <Card className="mt-6 p-5 sm:p-7"><div className="flex items-center gap-4"><Skeleton className="h-16 w-16 rounded-full" /><div className="flex-1 space-y-2"><Skeleton className="h-4 w-40" /><Skeleton className="h-3 w-28" /></div></div><Skeleton className="mt-5 h-8 w-3/4" /><div className="mt-4 space-y-2.5">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className={clsx("h-4", i === 3 ? "w-1/2" : "w-full")} />)}</div></Card>;
}
