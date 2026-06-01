"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { forumApi } from "@/lib/api/forum";
import { assetsApi } from "@/lib/api/assets";
import { useAuth, useToast } from "@/lib/providers";
import { ArrowLeft, ImagePlus, Eye, Pencil } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { EmptyState } from "@/components/ui/empty-state";
import { MarkdownContent } from "@/components/post-content";
import { qk } from "@/lib/query-keys";

const schema = z.object({
  title: z.string().min(3, "Title too short").max(200),
  categoryId: z.string().min(1, "Select a category"),
  content: z.string().min(10, "Content too short"),
});

type FormData = z.infer<typeof schema>;

export default function NewThreadPage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const router = useRouter();
  const [error, setError] = useState("");
  const [showPreview, setShowPreview] = useState(false);
  const [uploading, setUploading] = useState(false);
  const contentRef = useRef<HTMLTextAreaElement | null>(null);

  const { register, handleSubmit, setValue, getValues, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const { ref: contentFieldRef, ...contentField } = register("content");
  const content = watch("content") ?? "";

  // Upload an image as an asset, then insert a markdown image tag at the cursor.
  const insertImage = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    setError("");
    try {
      const res = await assetsApi.uploadInlineImage(file);
      if (res.success && res.data) {
        const url = res.data;
        const md = `\n![${file.name}](${url})\n`;
        const ta = contentRef.current;
        const current = getValues("content") ?? "";
        const at = ta ? ta.selectionStart : current.length;
        setValue("content", current.slice(0, at) + md + current.slice(at), { shouldValidate: true });
      } else {
        toast("Image upload failed.", "error");
      }
    } catch {
      toast("Image upload failed.", "error");
    } finally {
      setUploading(false);
    }
  };

  const { data: categories = [] } = useQuery({
    queryKey: qk.forum.categories(),
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data ?? [] : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: async (data: FormData) => {
      const res = await forumApi.createThread({ title: data.title, categoryId: Number(data.categoryId), content: data.content });
      return res;
    },
    onSuccess: (res) => {
      if (res.success && res.data) {
        router.push(`/forum/${res.data.id}`);
      }
    },
    onError: () => {
      setError("Failed to create thread");
    },
  });

  if (!user) {
    return (
      <PageShell size="md">
        <EmptyState
          title="Sign in required"
          description="You must be signed in to create a thread."
          action={<Link href="/login"><Button variant="secondary">Sign in</Button></Link>}
        />
      </PageShell>
    );
  }

  const onSubmit = (data: FormData) => {
    setError("");
    createMutation.mutate(data);
  };

  return (
    <PageShell size="md">
      <Link href="/forum" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Forum
      </Link>
      <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">New Thread</h1>

      {error && (
        <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
          {error}
        </div>
      )}

      <Card className="mt-6 p-5 sm:p-6">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <Input label="Title" {...register("title")} error={errors.title?.message} />
          <Select label="Category" {...register("categoryId")} error={errors.categoryId?.message}>
            <option value="">Select category...</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </Select>
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <label className="block text-sm font-medium text-fg-muted">Content (Markdown)</label>
              <div className="flex items-center gap-2">
                <label className={`inline-flex cursor-pointer items-center gap-1.5 rounded-md border border-border-strong px-2.5 py-1 text-xs text-fg-muted transition-colors hover:border-accent/60 hover:text-fg ${uploading ? "pointer-events-none opacity-60" : ""}`}>
                  <ImagePlus size={14} /> {uploading ? "Uploading…" : "Insert image"}
                  <input type="file" accept="image/*" className="hidden" disabled={uploading}
                    onChange={(e) => { insertImage(e.target.files?.[0]); e.target.value = ""; }} />
                </label>
                <button type="button" onClick={() => setShowPreview((v) => !v)}
                  className="inline-flex items-center gap-1.5 rounded-md border border-border-strong px-2.5 py-1 text-xs text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
                  {showPreview ? <><Pencil size={14} /> Edit</> : <><Eye size={14} /> Preview</>}
                </button>
              </div>
            </div>
            {showPreview ? (
              <div className="min-h-[14rem] rounded-lg border border-border bg-surface-2 px-3 py-2">
                {content.trim()
                  ? <MarkdownContent content={content} />
                  : <p className="text-sm text-fg-subtle">Nothing to preview yet.</p>}
              </div>
            ) : (
              <textarea
                {...contentField}
                ref={(el) => { contentFieldRef(el); contentRef.current = el; }}
                rows={10}
                placeholder="Write in Markdown. Use Insert image to upload and embed images."
                className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
              />
            )}
            {errors.content && <p className="text-xs text-danger">{errors.content.message}</p>}
          </div>
          <Button type="submit" loading={createMutation.isPending}>Create Thread</Button>
        </form>
      </Card>
    </PageShell>
  );
}
