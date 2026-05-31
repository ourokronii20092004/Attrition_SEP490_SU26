"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { Pager } from "./Pager";

export function ReportsQueue() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.admin.forum.reports(page),
    queryFn: async () => {
      const res = await forumApi.getReports({ status: "Pending", page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const reports = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.forum.reports() });

  const dismissMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.dismissReport(id); },
    onSuccess: invalidate,
  });
  const removePostMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => { await forumApi.removePost(postId, { reason }); },
    onSuccess: invalidate,
  });

  const dismiss = (id: string) => dismissMutation.mutate(id);
  const removePost = (postId: string, reason: string) => removePostMutation.mutate({ postId, reason });

  if (loading) return <PageLoader />;
  if (!reports.length) return <p className="py-8 text-center text-fg-muted">No pending reports.</p>;

  return (
    <div className="space-y-4">
      {reports.map((r) => (
        <div key={r.id} className="card p-4">
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <p className="font-medium text-fg">Post by {r.authorName}</p>
              <p className="mt-1 line-clamp-3 whitespace-pre-wrap text-sm text-fg">{r.postContent}</p>
              <p className="mt-2 text-sm text-fg-muted">Reported by {r.reporterName} · {formatDate(r.createdAt)}</p>
              <p className="mt-1 text-sm text-fg-subtle">Reason: {r.reason}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" variant="danger" onClick={() => removePost(r.postId, r.reason)}>Remove Post</Button>
              <Button size="sm" variant="secondary" onClick={() => dismiss(r.id)}>Dismiss</Button>
            </div>
          </div>
        </div>
      ))}
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}
