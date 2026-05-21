"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import styles from "../../admin.module.css";

interface WikiCategory {
  id: number;
  name: string;
  slug: string;
  description: string;
  articleCount?: number;
}

export default function AdminWikiCategoriesPage() {
  const toast = useToast();
  const [categories, setCategories] = useState<WikiCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [description, setDescription] = useState("");

  const fetchCategories = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WikiCategory[]>("/admin/wiki/categories");
      if (res.success && res.data) setCategories(res.data);
    } catch {
      toast.error("Failed to load categories");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchCategories(); }, [fetchCategories]);

  const resetForm = () => {
    setShowForm(false); setEditId(null);
    setName(""); setSlug(""); setDescription("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editId) {
        await api.put(`/admin/wiki/categories/${editId}`, { name, slug, description });
        toast.success("Category updated");
      } else {
        await api.post("/admin/wiki/categories", { name, slug, description });
        toast.success("Category created");
      }
      resetForm();
      fetchCategories();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save category");
    }
  };

  const handleEdit = (cat: WikiCategory) => {
    setEditId(cat.id); setName(cat.name); setSlug(cat.slug);
    setDescription(cat.description || ""); setShowForm(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm("Delete this category?")) return;
    try {
      await api.delete(`/admin/wiki/categories/${id}`);
      setCategories((prev) => prev.filter((c) => c.id !== id));
      toast.success("Category deleted");
    } catch {
      toast.error("Failed to delete category");
    }
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Wiki Categories</h1>
        <button className="btn btn-primary btn-sm" onClick={() => { resetForm(); setShowForm(true); }}>
          Add Category
        </button>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} style={{
          background: "var(--surface)", border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)", padding: "var(--space-5)",
          marginBottom: "var(--space-6)", display: "flex", flexDirection: "column", gap: "var(--space-4)"
        }}>
          <div className="input-group">
            <label className="input-label">Name</label>
            <input className="input" value={name} onChange={(e) => { setName(e.target.value); if (!editId) setSlug(e.target.value.toLowerCase().replace(/[^a-z0-9]+/g, "-")); }} required />
          </div>
          <div className="input-group">
            <label className="input-label">Slug</label>
            <input className="input" value={slug} onChange={(e) => setSlug(e.target.value)} required />
          </div>
          <div className="input-group">
            <label className="input-label">Description</label>
            <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
          </div>
          <div style={{ display: "flex", gap: "var(--space-3)" }}>
            <button type="submit" className="btn btn-primary btn-sm">{editId ? "Update" : "Create"}</button>
            <button type="button" className="btn btn-secondary btn-sm" onClick={resetForm}>Cancel</button>
          </div>
        </form>
      )}

      <div className={styles.adminTableWrapper}>
        <div className="table-wrapper" style={{ border: "none", borderRadius: 0 }}>
          <table className="table">
            <thead><tr><th>Name</th><th>Slug</th><th>Description</th><th>Articles</th><th>Actions</th></tr></thead>
            <tbody>
              {loading ? (
                Array.from({ length: 3 }).map((_, i) => (
                  <tr key={i}>{Array.from({ length: 5 }).map((_, j) => (<td key={j}><div className="skeleton skeleton-text" /></td>))}</tr>
                ))
              ) : categories.length > 0 ? (
                categories.map((cat) => (
                  <tr key={cat.id}>
                    <td><strong>{cat.name}</strong></td>
                    <td><code>{cat.slug}</code></td>
                    <td>{cat.description || "—"}</td>
                    <td>{cat.articleCount ?? 0}</td>
                    <td style={{ display: "flex", gap: "var(--space-2)" }}>
                      <button className="btn btn-secondary btn-sm" style={{ fontSize: "var(--text-xs)" }} onClick={() => handleEdit(cat)}>Edit</button>
                      <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }} onClick={() => handleDelete(cat.id)}>Delete</button>
                    </td>
                  </tr>
                ))
              ) : (
                <tr><td colSpan={5} style={{ textAlign: "center", padding: "var(--space-8)" }}>No categories yet</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
