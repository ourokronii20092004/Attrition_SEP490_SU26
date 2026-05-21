"use client";

import React, { useState, useEffect, useRef, FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import styles from "../forum.module.css";

interface ForumCategory {
  id: number;
  name: string;
  slug: string;
}

export default function NewThreadPage() {
  const router = useRouter();
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const toast = useToast();

  const [categories, setCategories] = useState<ForumCategory[]>([]);
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Auth guard — redirect to login if not authenticated
  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.replace("/login?redirect=forum/new");
    }
  }, [authLoading, isAuthenticated, router]);

  // Load categories
  useEffect(() => {
    api.get<ForumCategory[]>("/forum/categories").then((res) => {
      // Handle raw array or { success, data } wrapper
      const cats = Array.isArray(res) ? res : (res.success && res.data ? res.data : []);
      setCategories(cats as ForumCategory[]);
      if ((cats as ForumCategory[]).length > 0) setCategoryId((cats as ForumCategory[])[0].id);
    });
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");

    if (!title.trim()) {
      setError("Please enter a title");
      return;
    }
    if (!content.trim()) {
      setError("Please enter some content");
      return;
    }
    if (!categoryId) {
      setError("Please select a category");
      return;
    }

    setLoading(true);
    try {
      const res = await api.post("/forum/threads", {
        categoryId,
        title: title.trim(),
        content: content.trim(),
      });
      // API may return { success: true, data: threadId } or throw on error
      toast.success("Thread created!");
      router.push("/forum");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create thread");
    } finally {
      setLoading(false);
    }
  };

  if (authLoading || !isAuthenticated) {
    return (
      <div className="page">
        <div className="container" style={{ display: "flex", justifyContent: "center", paddingTop: "20vh" }}>
          <span className="text-muted text-sm">Loading...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="container">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <Link href="/forum">Forum</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">New Thread</span>
          </div>
          <h1>Create a New Thread</h1>
        </div>

        <form onSubmit={handleSubmit} className={styles.newThreadForm}>
          {error && (
            <div
              style={{
                padding: "var(--space-3) var(--space-4)",
                background: "var(--danger-bg)",
                border: "1px solid var(--danger-border)",
                borderRadius: "var(--radius-md)",
                color: "var(--danger)",
                fontSize: "var(--text-sm)",
                marginBottom: "var(--space-5)",
              }}
              role="alert"
            >
              {error}
            </div>
          )}

          <div className="input-group" style={{ marginBottom: "var(--space-5)" }}>
            <label htmlFor="category" className="input-label">
              Category
            </label>
            <select
              id="category"
              className="input"
              value={categoryId}
              onChange={(e) => setCategoryId(Number(e.target.value))}
              disabled={loading}
            >
              <option value="" disabled>
                Select a category
              </option>
              {categories.map((cat) => (
                <option key={cat.id} value={cat.id}>
                  {cat.name}
                </option>
              ))}
            </select>
          </div>

          <div className="input-group" style={{ marginBottom: "var(--space-5)" }}>
            <label htmlFor="title" className="input-label">
              Title
            </label>
            <input
              id="title"
              type="text"
              className="input"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="What's on your mind?"
              disabled={loading}
              maxLength={200}
            />
          </div>

          <div className="input-group" style={{ marginBottom: "var(--space-6)" }}>
            <label htmlFor="content" className="input-label">
              Content
            </label>
            <textarea
              id="content"
              className="input"
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="Share your thoughts, questions, or ideas..."
              disabled={loading}
              rows={8}
              style={{ minHeight: "200px" }}
            />
            <div style={{ marginTop: "var(--space-2)", display: "flex", alignItems: "center", gap: "var(--space-3)" }}>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                style={{ display: "none" }}
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  setUploading(true);
                  try {
                    const formData = new FormData();
                    formData.append("file", file);
                    const res = await api.upload<string>("/users/upload/image", formData);
                    if (res.success && res.data) {
                      const imgUrl = res.data.startsWith("http") ? res.data : `https://attrition.hault.io.vn${res.data}`;
                      setContent((prev) => prev + `\n![image](${imgUrl})\n`);
                      toast.success("Image uploaded");
                    } else {
                      toast.error("Failed to upload image");
                    }
                  } catch {
                    toast.error("Failed to upload image");
                  } finally {
                    setUploading(false);
                    if (fileInputRef.current) fileInputRef.current.value = "";
                  }
                }}
              />
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => fileInputRef.current?.click()}
                disabled={uploading || loading}
              >
                {uploading ? "Uploading..." : "📷 Add Image"}
              </button>
              <span style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>Max 10MB · JPG, PNG, GIF, WebP</span>
            </div>
          </div>

          <div className="flex gap-3">
            <button
              type="submit"
              className={cn("btn btn-primary btn-md", loading && "btn-loading")}
              disabled={loading}
            >
              Post Thread
            </button>
            <Link href="/forum" className="btn btn-secondary btn-md">
              Cancel
            </Link>
          </div>
        </form>
      </div>
    </div>
  );
}
