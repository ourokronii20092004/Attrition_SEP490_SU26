"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn } from "@/lib/utils";
import styles from "../../admin.module.css";

interface ForumReport {
  id: string;
  postId: string;
  postContent: string;
  authorName: string;
  reporterName: string;
  reason: string;
  status: string; // Pending, Dismissed, Resolved
  createdAt: string;
}

export default function AdminForumReportsPage() {
  const toast = useToast();
  const [reports, setReports] = useState<ForumReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>("Pending");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchReports = useCallback(async (status: string, pageNum: number) => {
    setLoading(true);
    try {
      const res = await api.get<ForumReport[]>(
        "/admin/forum/reports", { status, page: pageNum, pageSize }
      );
      if (res.success && res.data) {
        setReports(res.data);
        setTotalCount(res.totalCount ?? res.data.length);
      }
    } catch {
      toast.error("Failed to load reports");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchReports(statusFilter, page); }, [fetchReports, statusFilter, page]);

  const handleDismiss = async (id: string) => {
    try {
      await api.put(`/admin/forum/reports/${id}/dismiss`);
      setReports((prev) => prev.map((r) => r.id === id ? { ...r, status: "Dismissed" } : r));
      toast.success("Report dismissed");
    } catch {
      toast.error("Failed to dismiss report");
    }
  };

  const handleRemovePost = async (postId: string, reportId: string) => {
    const reason = prompt("Reason for removing this post:");
    if (reason === null) return;
    try {
      await api.post(`/admin/forum/posts/${postId}/remove`, { reason: reason || "Removed by admin due to user report" });
      setReports((prev) =>
        prev.map((r) => (r.postId === postId ? { ...r, status: "Resolved" } : r))
      );
      toast.success("Post removed and reports resolved");
    } catch {
      toast.error("Failed to remove post");
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Forum Moderation Reports</h1>
        <div style={{ display: "flex", gap: "var(--space-2)" }}>
          <button
            className={cn("btn btn-sm", statusFilter === "Pending" ? "btn-primary" : "btn-secondary")}
            onClick={() => { setStatusFilter("Pending"); setPage(1); }}
          >
            Pending
          </button>
          <button
            className={cn("btn btn-sm", statusFilter === "Dismissed" ? "btn-primary" : "btn-secondary")}
            onClick={() => { setStatusFilter("Dismissed"); setPage(1); }}
          >
            Dismissed
          </button>
          <button
            className={cn("btn btn-sm", statusFilter === "Resolved" ? "btn-primary" : "btn-secondary")}
            onClick={() => { setStatusFilter("Resolved"); setPage(1); }}
          >
            Resolved
          </button>
          <button
            className={cn("btn btn-sm", statusFilter === "" ? "btn-primary" : "btn-secondary")}
            onClick={() => { setStatusFilter(""); setPage(1); }}
          >
            All Reports
          </button>
        </div>
      </div>

      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <span className="text-sm text-muted">Showing {totalCount} moderation reports</span>
        </div>
        <div className="table-wrapper" style={{ border: "none", borderRadius: 0 }}>
          <table className="table">
            <thead>
              <tr>
                <th>Reported Post</th>
                <th>Post Author</th>
                <th>Reporter</th>
                <th>Reason</th>
                <th>Status</th>
                <th>Reported Date</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 7 }).map((_, j) => (
                      <td key={j}><div className="skeleton skeleton-text" /></td>
                    ))}
                  </tr>
                ))
              ) : reports.length > 0 ? (
                reports.map((r) => (
                  <tr key={r.id} style={{ opacity: r.status !== "Pending" ? 0.6 : 1 }}>
                    <td style={{ maxWidth: 260, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {r.postContent}
                    </td>
                    <td>{r.authorName}</td>
                    <td>{r.reporterName}</td>
                    <td><span className="text-sm">{r.reason}</span></td>
                    <td>
                      {r.status === "Pending" && <span className="badge badge-warning badge-dot">Pending</span>}
                      {r.status === "Dismissed" && <span className="badge badge-secondary badge-dot">Dismissed</span>}
                      {r.status === "Resolved" && <span className="badge badge-success badge-dot">Resolved</span>}
                    </td>
                    <td>{formatDate(r.createdAt)}</td>
                    <td>
                      {r.status === "Pending" ? (
                        <div style={{ display: "flex", gap: "var(--space-1)" }}>
                          <button
                            className="btn btn-secondary btn-sm"
                            style={{ fontSize: "var(--text-xs)" }}
                            onClick={() => handleDismiss(r.id)}
                          >
                            Dismiss
                          </button>
                          <button
                            className="btn btn-danger btn-sm"
                            style={{ fontSize: "var(--text-xs)" }}
                            onClick={() => handleRemovePost(r.postId, r.id)}
                          >
                            Remove Post
                          </button>
                        </div>
                      ) : (
                        <span className="text-muted text-xs">—</span>
                      )}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={7} style={{ textAlign: "center", padding: "var(--space-8)" }}>
                    No reports found
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        {totalPages > 1 && (
          <div className={styles.adminTableFooter}>
            <span>Page {page} of {totalPages}</span>
            <div className="pagination">
              <button className="pagination-btn" disabled={page === 1} onClick={() => setPage(page - 1)}>←</button>
              <button className="pagination-btn" disabled={page === totalPages} onClick={() => setPage(page + 1)}>→</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
