"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate } from "@/lib/utils";
import styles from "../../admin.module.css";

interface WikiArticle {
  id: string;
  title: string;
  slug: string;
  categoryName: string;
  authorName: string;
  createdAt: string;
  updatedAt: string;
}

export default function AdminWikiContributionsPage() {
  const toast = useToast();
  const [articles, setArticles] = useState<WikiArticle[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchArticles = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WikiArticle[]>("/admin/wiki/articles");
      if (res.success && res.data) {
        setArticles(Array.isArray(res.data) ? res.data : []);
      }
    } catch {
      setArticles([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchArticles(); }, [fetchArticles]);

  const handleDelete = async (id: string) => {
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
      <h1 style={{ marginBottom: "var(--space-6)" }}>Wiki Contributions</h1>

      <div style={{ 
        background: "var(--surface)", border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)", padding: "var(--space-5)",
        marginBottom: "var(--space-6)", color: "var(--text-secondary)",
        fontSize: "var(--text-sm)"
      }}>
        <strong>ℹ️ Note:</strong> This view shows all wiki articles and their latest edits. 
        You can view articles on the public wiki or delete them here.
      </div>

      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <span className="text-sm text-muted">{articles.length} articles</span>
        </div>
        <table className="table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Category</th>
              <th>Author</th>
              <th>Last Updated</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={i}>{Array.from({ length: 5 }).map((_, j) => (
                  <td key={j}><div className="skeleton skeleton-text" /></td>
                ))}</tr>
              ))
            ) : articles.length > 0 ? (
              articles.map((a) => (
                <tr key={a.id}>
                  <td><strong>{a.title}</strong></td>
                  <td>{a.categoryName || "—"}</td>
                  <td>{a.authorName || "—"}</td>
                  <td>{formatDate(a.updatedAt || a.createdAt)}</td>
                  <td>
                    <div style={{ display: "flex", gap: "var(--space-1)" }}>
                      <a href={`/wiki/${a.slug || a.id}`} target="_blank" rel="noopener noreferrer"
                        className="btn btn-secondary btn-sm" style={{ fontSize: "var(--text-xs)" }}>View</a>
                      <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }}
                        onClick={() => handleDelete(a.id)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr><td colSpan={5} style={{ textAlign: "center", padding: "var(--space-8)" }}>
                No contributions yet
              </td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
