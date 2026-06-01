"use client";

import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation } from "@tanstack/react-query";
import { wikiApi } from "@/lib/api/wiki";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { qk } from "@/lib/query-keys";
import type { WikiArticleListDto, WikiCategoryDto } from "@/lib/types";

const articleSchema = z.object({
  title: z.string()
    .refine((v) => v.trim().length >= 3 && v.trim().length <= 100, "Title must be between 3 and 100 characters."),
  categoryId: z.coerce.number(),
  content: z.string()
    .refine((v) => v.trim().length >= 20, "Content must be at least 20 characters."),
  status: z.string(),
});

type ArticleFormValues = z.infer<typeof articleSchema>;

export function ArticleEditor({ article, categories, onDone, onCancel, onDirtyChange }: {
  article: WikiArticleListDto | null;
  categories: WikiCategoryDto[];
  onDone: () => void;
  onCancel: () => void;
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const [error, setError] = useState("");
  const contentRef = useRef<HTMLTextAreaElement | null>(null);

  const {
    register, handleSubmit, setValue, getValues, watch,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ArticleFormValues>({
    resolver: zodResolver(articleSchema),
    defaultValues: {
      title: article?.title ?? "",
      categoryId: categories[0]?.id ?? 0,
      content: "",
      status: "Published",
    },
  });

  const { ref: contentFieldRef, ...contentField } = register("content");
  const content = watch("content");

  // For an existing article, pull current content (list DTO doesn't carry it).
  const { data: existing } = useQuery({
    queryKey: qk.wiki.article(article?.slug),
    enabled: !!article,
    queryFn: async () => {
      const res = await wikiApi.getArticle(article!.slug);
      return res.success ? res.data : null;
    },
  });

  useEffect(() => {
    if (existing) { setValue("content", existing.content); setValue("status", existing.status); }
  }, [existing, setValue]);

  // Surface dirty state so the Modal guards against an accidental backdrop-click discard (QOLF-6).
  useEffect(() => { onDirtyChange?.(isDirty); }, [isDirty, onDirtyChange]);

  // Upload an inline image (no gallery Asset row — same as forum), then insert a markdown image
  // tag at the cursor. Using uploadInlineImage (not assetsApi.create) keeps editor images out of
  // the public gallery, which otherwise accumulated a duplicate per edit (PROB-3).
  const insertImageMutation = useMutation({
    mutationFn: async (file: File) => assetsApi.uploadInlineImage(file),
    onSuccess: (res) => {
      if (res.success && res.data) {
        const url = resolveMediaUrl(res.data) ?? res.data;
        const md = `\n![image](${url})\n`;
        const ta = contentRef.current;
        const current = getValues("content");
        const at = ta ? ta.selectionStart : current.length;
        setValue("content", current.slice(0, at) + md + current.slice(at));
      } else {
        setError(res.error || "Image upload failed.");
      }
    },
    onError: (e) => {
      setError(parseApiError(e, "Image upload failed."));
    },
  });

  const insertImage = (file: File) => {
    setError("");
    insertImageMutation.mutate(file);
  };
  const imgUploading = insertImageMutation.isPending;

  const onSubmit = handleSubmit(async (values) => {
    setError("");
    try {
      if (article) {
        await wikiApi.updateArticle(article.id, { title: values.title, content: values.content, status: values.status });
      } else {
        await wikiApi.createArticle({ title: values.title, categoryId: values.categoryId, content: values.content, status: values.status });
      }
      onDone();
    } catch (e) {
      setError(parseApiError(e, "Failed to save article."));
    }
  });

  return (
    <div className="space-y-3">
      <Input label="Title" error={errors.title?.message} {...register("title")} />
      {!article && (
        <Select label="Category" {...register("categoryId")}>
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
          {...contentField}
          ref={(el) => { contentFieldRef(el); contentRef.current = el; }}
          rows={12}
          placeholder="Write in Markdown. Use the Insert image button to upload and embed images."
          className="w-full resize-y rounded-md border border-border bg-surface-2 px-3 py-2 font-mono text-sm text-fg outline-none focus:border-accent"
        />
        {errors.content && <p className="text-xs text-danger">{errors.content.message}</p>}
        <p className="text-xs text-fg-subtle">{content.trim().length} characters (minimum 20)</p>
      </div>
      <Select label="Status" {...register("status")}>
        <option value="Published">Published</option>
        <option value="Draft">Draft</option>
      </Select>
      {error && <p className="text-sm text-danger">{error}</p>}
      <div className="flex gap-2">
        <Button onClick={onSubmit} loading={isSubmitting} disabled={!watch("title").trim()}>{article ? "Update" : "Create"}</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
