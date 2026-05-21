"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn, debounce } from "@/lib/utils";
import styles from "../admin.module.css";

interface WikiArticle {
  id: number;
  title: string;
  slug: string;
  categoryName: string;
  authorUsername: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export default function AdminWikiPage() {
  const toast = useToast();
  const [articles, setArticles] = useState<WikiArticle[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");

  const fetchArticles = useCallback(async (searchTerm: string) => {
    setLoading(true);
    try {
      const params: Record<string, string> = {};
      if (searchTerm) params.search = searchTerm;
      const res = await api.get<WikiArticle[]>("/admin/wiki/articles", params);
      if (res.success && res.data) {
        setArticles(Array.isArray(res.data) ? res.data : []);
      }
    } catch {
      toast.error("Failed to load articles");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchArticles(search); }, [fetchArticles, search]);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(
    debounce((term: string) => setSearch(term), 300), []
  );

  const handleDelete = async (id: number) => {
    if (!confirm("Delete this article?")) return;
    try {
      await api.delete(`/admin/wiki/articles/${id}`);
      setArticles((prev) => prev.filter((a) => a.id !== id));
      toast.success("Article deleted");
    } catch {
      toast.error("Failed to delete article");
    }
  };

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Wiki Articles</h1>
      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <input
            type="text" className="input"
            placeholder="Search articles..."
            onChange={(e) => debouncedSearch(e.target.value)}
            style={{ maxWidth: 300 }}
          />
          <span className="text-sm text-muted">{articles.length} articles</span>
        </div>
        <div className="table-wrapper" style={{ border: "none", borderRadius: 0 }}>
          <table className="table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Category</th>
                <th>Author</th>
                <th>Status</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>{Array.from({ length: 6 }).map((_, j) => (
                    <td key={j}><div className="skeleton skeleton-text" /></td>
                  ))}</tr>
                ))
              ) : articles.length > 0 ? (
                articles.map((a) => (
                  <tr key={a.id}>
                    <td><strong>{a.title}</strong></td>
                    <td>{a.categoryName || "—"}</td>
                    <td>{a.authorUsername || "—"}</td>
                    <td><span className="badge badge-default">{a.status || "published"}</span></td>
                    <td>{formatDate(a.updatedAt || a.createdAt)}</td>
                    <td>
                      <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }}
                        onClick={() => handleDelete(a.id)}>Delete</button>
                    </td>
                  </tr>
                ))
              ) : (
                <tr><td colSpan={6} style={{ textAlign: "center", padding: "var(--space-8)" }}>No articles found</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
