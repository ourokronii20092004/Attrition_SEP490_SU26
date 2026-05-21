"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn, debounce } from "@/lib/utils";
import styles from "../../admin.module.css";

interface ForumPost {
  id: number;
  content: string;
  authorUsername: string;
  threadTitle: string;
  threadId: number;
  isRemoved: boolean;
  createdAt: string;
}

export default function AdminForumPostsPage() {
  const toast = useToast();
  const [posts, setPosts] = useState<ForumPost[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchPosts = useCallback(async (searchTerm: string, pageNum: number) => {
    setLoading(true);
    try {
      const res = await api.get<ForumPost[]>(
        "/admin/forum/posts", { search: searchTerm, page: pageNum, pageSize }
      );
      if (res.success && res.data) {
        setPosts(res.data);
        setTotalCount(res.totalCount ?? res.data.length);
      }
    } catch {
      toast.error("Failed to load posts");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchPosts(search, page); }, [fetchPosts, search, page]);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(debounce((term: string) => { setPage(1); setSearch(term); }, 300), []);

  const handleRemove = async (id: number) => {
    const reason = prompt("Reason for removal:");
    if (reason === null) return;
    try {
      await api.post(`/admin/forum/posts/${id}/remove`, { reason: reason || "Removed by admin" });
      setPosts((prev) => prev.map((p) => p.id === id ? { ...p, isRemoved: true } : p));
      toast.success("Post removed");
    } catch { toast.error("Failed to remove post"); }
  };

  const handleRestore = async (id: number) => {
    try {
      await api.post(`/admin/forum/posts/${id}/restore`);
      setPosts((prev) => prev.map((p) => p.id === id ? { ...p, isRemoved: false } : p));
      toast.success("Post restored");
    } catch { toast.error("Failed to restore post"); }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Forum Posts</h1>
      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <input type="text" className="input" placeholder="Search posts..." onChange={(e) => debouncedSearch(e.target.value)} style={{ maxWidth: 300 }} />
          <span className="text-sm text-muted">{totalCount} posts</span>
        </div>
        <div className="table-wrapper" style={{ border: "none", borderRadius: 0 }}>
          <table className="table">
            <thead><tr><th>Content</th><th>Thread</th><th>Author</th><th>Status</th><th>Date</th><th>Actions</th></tr></thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>{Array.from({ length: 6 }).map((_, j) => (<td key={j}><div className="skeleton skeleton-text" /></td>))}</tr>
                ))
              ) : posts.length > 0 ? (
                posts.map((p) => (
                  <tr key={p.id} style={{ opacity: p.isRemoved ? 0.5 : 1 }}>
                    <td style={{ maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{p.content}</td>
                    <td>{p.threadTitle || `#${p.threadId}`}</td>
                    <td>{p.authorUsername || "—"}</td>
                    <td>{p.isRemoved ? <span className="badge badge-danger badge-dot">Removed</span> : <span className="badge badge-success badge-dot">Active</span>}</td>
                    <td>{formatDate(p.createdAt)}</td>
                    <td>
                      {p.isRemoved ? (
                        <button className="btn btn-secondary btn-sm" style={{ fontSize: "var(--text-xs)" }} onClick={() => handleRestore(p.id)}>Restore</button>
                      ) : (
                        <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }} onClick={() => handleRemove(p.id)}>Remove</button>
                      )}
                    </td>
                  </tr>
                ))
              ) : (
                <tr><td colSpan={6} style={{ textAlign: "center", padding: "var(--space-8)" }}>No posts found</td></tr>
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
