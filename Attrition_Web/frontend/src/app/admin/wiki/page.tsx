"use client";

import { useEffect, useRef, useState } from "react";
import { wikiApi } from "@/lib/api/wiki";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import type { WikiArticleListDto, WikiCategoryDto, WikiContributionDto } from "@/lib/types";

type Tab = "articles" | "queue" | "categories";

export default function AdminWikiPage() {
  const [tab, setTab] = useState<Tab>("articles");
  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Wiki Management</h1>
      <div className="mt-4 flex gap-1 border-b border-border">
        {(["articles", "queue", "categories"] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`relative px-4 py-2.5 text-sm font-medium capitalize transition-colors ${tab === t ? "text-accent" : "text-fg-muted hover:text-fg"}`}
          >
            {t === "queue" ? "Contribution Queue" : t}
            {tab === t && <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent" />}
          </button>
        ))}
      </div>
      <div className="mt-6">
        {tab === "articles" && <ArticlesAdmin />}
        {tab === "queue" && <ContributionQueue />}
        {tab === "categories" && <CategoriesAdmin />}
      </div>
    </div>
  );
}

function ArticlesAdmin() {
  const [articles, setArticles] = useState<WikiArticleListDto[]>([]);
  const [categories, setCategories] = useState<WikiCategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<WikiArticleListDto | "new" | null>(null);

  const load = () => {
    setLoading(true);
    Promise.all([wikiApi.getArticles({ pageSize: 100 }), wikiApi.getCategories()])
      .then(([a, c]) => {
        if (a.success) setArticles(a.data.items);
        if (c.success) setCategories(c.data);
      })
      .finally(() => setLoading(false));
  };
  useEffect(load, []);

  const remove = async (id: string) => {
    if (!confirm("Delete this article?")) return;
    await wikiApi.deleteArticle(id);
    load();
  };

  if (loading) return <PageLoader />;

  return (
    <div>
      <div className="mb-4 flex justify-end">
        <Button onClick={() => setEditing("new")}>New Article</Button>
      </div>
      {editing && (
        <ArticleEditor
          article={editing === "new" ? null : editing}
          categories={categories}
          onDone={() => { setEditing(null); load(); }}
          onCancel={() => setEditing(null)}
        />
      )}
      <div className="space-y-2">
        {articles.map((a) => (
          <div key={a.id} className="card flex items-center justify-between p-4">
            <div className="min-w-0">
              <p className="truncate font-medium text-fg">{a.title}</p>
              <p className="text-xs text-fg-muted">{a.categorySlug} · {formatDate(a.updatedAt)}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" variant="secondary" onClick={() => setEditing(a)}>Edit</Button>
              <Button size="sm" variant="danger" onClick={() => remove(a.id)}>Delete</Button>
            </div>
          </div>
        ))}
        {!articles.length && <p className="py-8 text-center text-fg-muted">No articles yet.</p>}
      </div>
    </div>
  );
}

function CategoriesAdmin() {
  const [categories, setCategories] = useState<WikiCategoryDto[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    wikiApi.getCategories().then((r) => { if (r.success) setCategories(r.data); }).finally(() => setLoading(false));
  };
  useEffect(load, []);

  const create = async () => {
    if (!name.trim()) return;
    await wikiApi.createCategory({ name, description });
    setName(""); setDescription(""); load();
  };
  const remove = async (id: number) => {
    if (!confirm("Delete this category?")) return;
    await wikiApi.deleteCategory(id);
    load();
  };

  if (loading) return <PageLoader />;

  return (
    <div>
      <div className="card mb-6 space-y-3 p-4">
        <Input label="New category name" value={name} onChange={(e) => setName(e.target.value)} />
        <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
        <Button onClick={create} disabled={!name.trim()}>Add Category</Button>
      </div>
      <div className="space-y-2">
        {categories.map((c) => (
          <div key={c.id} className="card flex items-center justify-between p-4">
            <div>
              <p className="font-medium text-fg">{c.name}</p>
              <p className="text-xs text-fg-muted">{c.articleCount} articles · {c.slug}</p>
            </div>
            <Button size="sm" variant="danger" onClick={() => remove(c.id)} disabled={c.articleCount > 0}>Delete</Button>
          </div>
        ))}
      </div>
    </div>
  );
}

function ContributionQueue() {
  const [items, setItems] = useState<WikiContributionDto[]>([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    wikiApi.getContributions().then((r) => { if (r.success) setItems(r.data); }).finally(() => setLoading(false));
  };
  useEffect(load, []);

  const review = async (id: string, status: "Approved" | "Rejected") => {
    await wikiApi.reviewContribution(id, { status });
    load();
  };

  if (loading) return <PageLoader />;
  const pending = items.filter((c) => c.status === "Pending");
  if (!pending.length) return <p className="py-8 text-center text-fg-muted">No pending contributions.</p>;

  return (
    <div className="space-y-4">
      {pending.map((c) => (
        <div key={c.id} className="card p-4">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="font-medium text-fg">{c.articleTitle}</p>
              <p className="mt-1 text-sm text-fg-muted">by {c.contributorName} · {formatDate(c.submittedAt)}</p>
              <p className="mt-1 text-sm text-fg-subtle">{c.changeNote ?? "No note"}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" onClick={() => review(c.id, "Approved")}>Approve</Button>
              <Button size="sm" variant="danger" onClick={() => review(c.id, "Rejected")}>Reject</Button>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function ArticleEditor({ article, categories, onDone, onCancel }: {
  article: WikiArticleListDto | null;
  categories: WikiCategoryDto[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(article?.title ?? "");
  const [categoryId, setCategoryId] = useState<number>(categories[0]?.id ?? 0);
  const [content, setContent] = useState("");
  const [status, setStatus] = useState("Published");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [imgUploading, setImgUploading] = useState(false);
  const contentRef = useRef<HTMLTextAreaElement>(null);

  // For an existing article, pull current content (list DTO doesn't carry it).
  useEffect(() => {
    if (article) {
      wikiApi.getArticle(article.slug).then((r) => {
        if (r.success) { setContent(r.data.content); setStatus(r.data.status); }
      });
    }
  }, [article]);

  // Upload an image as an asset, then insert a markdown image tag at the cursor.
  const insertImage = async (file: File) => {
    setImgUploading(true);
    setError("");
    try {
      const res = await assetsApi.create(file, { assetType: "image", title: file.name });
      if (res.success && res.data) {
        const url = resolveMediaUrl(res.data.filePath) ?? res.data.filePath;
        const md = `\n![${res.data.title ?? "image"}](${url})\n`;
        const ta = contentRef.current;
        const at = ta ? ta.selectionStart : content.length;
        setContent((c) => c.slice(0, at) + md + c.slice(at));
      } else {
        setError(res.error || "Image upload failed.");
      }
    } catch (e) {
      setError(parseApiError(e, "Image upload failed."));
    } finally {
      setImgUploading(false);
    }
  };

  const save = async () => {
    // Mirror the server rules so the user sees the problem before a round-trip.
    if (title.trim().length < 3 || title.trim().length > 100) {
      setError("Title must be between 3 and 100 characters."); return;
    }
    if (content.trim().length < 20) {
      setError("Content must be at least 20 characters."); return;
    }
    setSaving(true); setError("");
    try {
      if (article) {
        await wikiApi.updateArticle(article.id, { title, content, status });
      } else {
        await wikiApi.createArticle({ title, categoryId, content, status });
      }
      onDone();
    } catch (e) {
      setError(parseApiError(e, "Failed to save article."));
      setSaving(false);
    }
  };

  return (
    <div className="card mb-6 space-y-3 p-4">
      <h3 className="font-medium text-fg">{article ? "Edit Article" : "New Article"}</h3>
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} error={title && title.trim().length < 3 ? "At least 3 characters" : undefined} />
      {!article && (
        <Select label="Category" value={categoryId} onChange={(e) => setCategoryId(+e.target.value)}>
          {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
      )}
      <div className="space-y-1.5">
        <div className="flex items-center justify-between">
          <label className="block text-xs font-medium uppercase tracking-wider text-fg-muted">Content (Markdown)</label>
          <label className="cursor-pointer rounded-md border border-border-strong px-2.5 py-1 text-xs text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
            {imgUploading ? "Uploading…" : "Insert image"}
            <input type="file" accept="image/*" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) insertImage(f); e.target.value = ""; }} />
          </label>
        </div>
        <textarea
          ref={contentRef}
          value={content}
          onChange={(e) => setContent(e.target.value)}
          rows={12}
          placeholder="Write in Markdown. Use the Insert image button to upload and embed images."
          className="w-full resize-y rounded-md border border-border bg-surface-2 px-3 py-2 font-mono text-sm text-fg outline-none focus:border-accent"
        />
        <p className="text-xs text-fg-subtle">{content.trim().length} characters (minimum 20)</p>
      </div>
      <Select label="Status" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="Published">Published</option>
        <option value="Draft">Draft</option>
      </Select>
      {error && <p className="text-sm text-danger">{error}</p>}
      <div className="flex gap-2">
        <Button onClick={save} loading={saving} disabled={!title.trim()}>{article ? "Update" : "Create"}</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
