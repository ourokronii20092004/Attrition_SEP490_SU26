"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CornerDownRight, EyeOff } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { RelativeTime } from "@/components/ui/relative-time";
import { PostContent } from "@/components/post-content";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";

// getPosts (public endpoint) filters out removed posts server-side, so this flat moderation list
// never receives IsRemoved posts. Restore is therefore not offered here — only Remove.
const POST_FETCH_SIZE = 100;

export default function AdminThreadPostsPage() {
  const { user } = useAuth();
  const params = useParams<{ id: string }>();
  const threadId = params.id;
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const [reply, setReply] = useState("");

  const { data, isPending: loading } = useQuery({
    queryKey: qk.forum.posts(threadId),
    enabled: user?.role === "Admin" && !!threadId,
    queryFn: async () => {
      const res = await forumApi.getPosts(threadId, { page: 1, pageSize: POST_FETCH_SIZE });
      return res.success ? res.data : null;
    },
  });

  const posts = data?.items ?? [];

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.forum.posts(threadId) });

  const removeMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => { await forumApi.removePost(postId, { reason }); },
    onSuccess: () => { toast("Post removed.", "success"); invalidate(); },
    onError: () => toast("Could not remove the post.", "error"),
  });
  const replyMutation = useMutation({
    mutationFn: async (content: string) => { await forumApi.createPost(threadId, { content }); },
    onSuccess: () => { setReply(""); toast("Reply posted.", "success"); invalidate(); },
    onError: () => toast("Could not post the reply.", "error"),
  });

  const removePost = async (postId: string) => {
    const reason = window.prompt("Reason for removing this post?");
    if (!reason?.trim()) return;
    if (!(await confirm({ message: "Remove this post from the thread?", danger: true, confirmLabel: "Remove" }))) return;
    removeMutation.mutate({ postId, reason: reason.trim() });
  };

  const submitReply = () => {
    if (!reply.trim()) return;
    replyMutation.mutate(reply.trim());
  };

  if (!user || user.role !== "Admin") return null;

  return (
    <div className="mx-auto max-w-3xl">
      <Link href="/admin/forum" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Forum Management
      </Link>
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">Thread Posts</h1>

      {loading ? (
        <PageLoader />
      ) : !posts.length ? (
        <p className="py-8 text-center text-fg-muted">No posts in this thread.</p>
      ) : (
        <div className="mt-6 space-y-3">
          {posts.map((p) => (
            <Card key={p.id} className="p-4">
              <div className="flex items-start gap-3">
                <Avatar src={p.authorAvatar} name={p.authorName} size="md" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link href={`/u/${encodeURIComponent(p.authorName)}`} className="text-sm font-medium text-fg transition-colors hover:text-accent">
                      {p.authorName}
                    </Link>
                    {p.authorRole === "Admin" && (
                      <span className="rounded bg-accent-soft px-1.5 py-0.5 text-xs font-medium text-accent">Admin</span>
                    )}
                    <span className="text-xs text-fg-subtle"><RelativeTime iso={p.createdAt} /></span>
                    {p.parentPostId && (
                      <span className="inline-flex items-center gap-1 text-xs text-fg-subtle">
                        <CornerDownRight size={12} /> reply
                      </span>
                    )}
                  </div>
                  <PostContent content={p.content} className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-fg" />

                  {p.attachments.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {p.attachments.map((url) => (
                        <a key={url} href={resolveMediaUrl(url) ?? ""} target="_blank" rel="noopener noreferrer">
                          <img src={resolveMediaUrl(url) ?? ""} alt="" className="max-h-48 rounded-lg border border-border object-cover" />
                        </a>
                      ))}
                    </div>
                  )}

                  <div className="mt-3 flex items-center gap-2">
                    <Button
                      size="sm"
                      variant="danger"
                      loading={removeMutation.isPending && removeMutation.variables?.postId === p.id}
                      onClick={() => removePost(p.id)}
                    >
                      <EyeOff size={14} className="mr-1" /> Remove
                    </Button>
                  </div>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <Card className="mt-6 p-4">
        <label className="text-sm font-medium text-fg">Reply as admin</label>
        <textarea
          value={reply}
          onChange={(e) => setReply(e.target.value)}
          placeholder="Write a reply..."
          rows={3}
          className="mt-2 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
        />
        <div className="mt-2 flex justify-end">
          <Button size="sm" onClick={submitReply} loading={replyMutation.isPending} disabled={!reply.trim()}>Reply</Button>
        </div>
      </Card>
    </div>
  );
}
