"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn, debounce } from "@/lib/utils";
import styles from "../admin.module.css";

interface ForumThread {
  id: number;
  title: string;
  categoryName: string;
  authorUsername: string;
  isPinned: boolean;
  isLocked: boolean;
  postCount: number;
  createdAt: string;
}

export default function AdminForumPage() {
  const toast = useToast();
  const [threads, setThreads] = useState<ForumThread[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchThreads = useCallback(async (searchTerm: string, pageNum: number) => {
    setLoading(true);
    try {
      const res = await api.get<ForumThread[]>(
        "/admin/forum/threads", { search: searchTerm, page: pageNum, pageSize }
      );
      if (res.success && res.data) {
        setThreads(res.data);
        setTotalCount(res.totalCount ?? res.data.length);
      }
    } catch {
      toast.error("Failed to load threads");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchThreads(search, page); }, [fetchThreads, search, page]);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(debounce((term: string) => { setPage(1); setSearch(term); }, 300), []);

  const handlePin = async (id: number, isPinned: boolean) => {
    try {
      await api.put(`/admin/forum/threads/${id}/pin`, { isPinned: !isPinned });
      setThreads((prev) => prev.map((t) => t.id === id ? { ...t, isPinned: !isPinned } : t));
      toast.success(isPinned ? "Unpinned" : "Pinned");
    } catch { toast.error("Failed to update"); }
  };

  const handleLock = async (id: number, isLocked: boolean) => {
    try {
      await api.put(`/admin/forum/threads/${id}/lock`, { isLocked: !isLocked });
      setThreads((prev) => prev.map((t) => t.id === id ? { ...t, isLocked: !isLocked } : t));
      toast.success(isLocked ? "Unlocked" : "Locked");
    } catch { toast.error("Failed to update"); }
  };

  const handleDelete = async (id: number) => {
    if (!confirm("Delete this thread and all its posts?")) return;
    try {
      await api.delete(`/admin/forum/threads/${id}`);
      setThreads((prev) => prev.filter((t) => t.id !== id));
      toast.success("Thread deleted");
    } catch { toast.error("Failed to delete"); }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Forum Threads</h1>
      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <input type="text" className="input" placeholder="Search threads..." onChange={(e) => debouncedSearch(e.target.value)} style={{ maxWidth: 300 }} />
          <span className="text-sm text-muted">{totalCount} threads</span>
        </div>
        <div className="table-wrapper" style={{ border: "none", borderRadius: 0 }}>
          <table className="table">
            <thead><tr><th>Title</th><th>Category</th><th>Author</th><th>Posts</th><th>Status</th><th>Created</th><th>Actions</th></tr></thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>{Array.from({ length: 7 }).map((_, j) => (<td key={j}><div className="skeleton skeleton-text" /></td>))}</tr>
                ))
              ) : threads.length > 0 ? (
                threads.map((t) => (
                  <tr key={t.id}>
                    <td><strong>{t.title}</strong></td>
                    <td>{t.categoryName || "—"}</td>
                    <td>{t.authorUsername || "—"}</td>
                    <td>{t.postCount}</td>
                    <td style={{ display: "flex", gap: "var(--space-1)", flexWrap: "wrap" }}>
                      {t.isPinned && <span className="badge badge-warning badge-dot">Pinned</span>}
                      {t.isLocked && <span className="badge badge-danger badge-dot">Locked</span>}
                      {!t.isPinned && !t.isLocked && <span className="badge badge-success badge-dot">Open</span>}
                    </td>
                    <td>{formatDate(t.createdAt)}</td>
                    <td>
                      <div style={{ display: "flex", gap: "var(--space-1)" }}>
                        <button className={cn("btn btn-sm", t.isPinned ? "btn-warning" : "btn-secondary")} style={{ fontSize: "var(--text-xs)" }} onClick={() => handlePin(t.id, t.isPinned)}>{t.isPinned ? "Unpin" : "Pin"}</button>
                        <button className={cn("btn btn-sm", t.isLocked ? "btn-warning" : "btn-secondary")} style={{ fontSize: "var(--text-xs)" }} onClick={() => handleLock(t.id, t.isLocked)}>{t.isLocked ? "Unlock" : "Lock"}</button>
                        <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }} onClick={() => handleDelete(t.id)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr><td colSpan={7} style={{ textAlign: "center", padding: "var(--space-8)" }}>No threads found</td></tr>
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
