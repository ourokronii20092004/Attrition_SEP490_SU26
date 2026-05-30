"use client";

import { useEffect, useState } from "react";
import { forumApi } from "@/lib/api/forum";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import type { AdminPostReportDto } from "@/lib/types";

export default function AdminForumPage() {
  const { user } = useAuth();
  const [reports, setReports] = useState<AdminPostReportDto[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchReports = () => {
    setLoading(true);
    forumApi
      .getReports()
      .then((res) => {
        if (res.success) setReports(res.data);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (user?.role !== "Admin") return;
    fetchReports();
  }, [user]);

  const handleDismiss = async (id: string) => {
    await forumApi.dismissReport(id);
    fetchReports();
  };

  const handleRemovePost = async (postId: string, reason: string) => {
    await forumApi.removePost(postId, { reason });
    fetchReports();
  };

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

  const pending = reports.filter((r) => r.status === "Pending");

  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Forum Moderation</h1>
      <p className="mt-2 text-fg-muted">{pending.length} pending reports</p>

      <div className="mt-6 space-y-4">
        {pending.map((r) => (
          <div key={r.id} className="card p-4">
            <div className="flex items-start justify-between gap-4">
              <div className="flex-1 min-w-0">
                <p className="font-medium text-fg">Post by {r.authorName}</p>
                <p className="mt-1 text-sm text-fg whitespace-pre-wrap line-clamp-3">{r.postContent}</p>
                <p className="mt-2 text-sm text-fg-muted">Reported by {r.reporterName} &middot; {formatDate(r.createdAt)}</p>
                <p className="mt-1 text-sm text-fg-subtle">Reason: {r.reason}</p>
              </div>
              <div className="flex shrink-0 gap-2">
                <Button size="sm" variant="danger" onClick={() => handleRemovePost(r.postId, r.reason)}>Remove Post</Button>
                <Button size="sm" variant="secondary" onClick={() => handleDismiss(r.id)}>Dismiss</Button>
              </div>
            </div>
          </div>
        ))}
        {pending.length === 0 && <p className="py-8 text-center text-fg-muted">No pending reports.</p>}
      </div>
    </div>
  );
}
