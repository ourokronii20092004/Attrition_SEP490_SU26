"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { forumApi } from "@/lib/api/forum";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ForumCategoryDto } from "@/lib/types";

const schema = z.object({
  title: z.string().min(3, "Title too short").max(200),
  categoryId: z.string().min(1, "Select a category"),
  content: z.string().min(10, "Content too short"),
});

type FormData = z.infer<typeof schema>;

export default function NewThreadPage() {
  const { user } = useAuth();
  const router = useRouter();
  const [categories, setCategories] = useState<ForumCategoryDto[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  useEffect(() => {
    forumApi.getCategories().then((res) => {
      if (res.success) setCategories(res.data);
    });
  }, []);

  if (!user) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <p className="text-fg-muted">You must be signed in to create a thread.</p>
        <Link href="/login" className="mt-4 inline-block text-accent hover:underline">Sign in</Link>
      </div>
    );
  }

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      const res = await forumApi.createThread({ title: data.title, categoryId: Number(data.categoryId), content: data.content });
      if (res.success && res.data) {
        router.push(`/forum/${res.data.id}`);
      }
    } catch {
      setError("Failed to create thread");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href="/forum" className="text-sm text-accent hover:underline">&larr; Forum</Link>
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">New Thread</h1>

      {error && <div className="mt-4 rounded-lg bg-danger/10 px-4 py-3 text-sm text-danger">{error}</div>}

      <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
        <Input label="Title" {...register("title")} error={errors.title?.message} />
        <div className="space-y-1">
          <label className="block text-sm font-medium text-fg-muted">Category</label>
          <select
            {...register("categoryId")}
            className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none"
          >
            <option value="">Select category...</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
          {errors.categoryId && <p className="text-xs text-danger">{errors.categoryId.message}</p>}
        </div>
        <div className="space-y-1">
          <label className="block text-sm font-medium text-fg-muted">Content</label>
          <textarea
            {...register("content")}
            rows={10}
            className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
          />
          {errors.content && <p className="text-xs text-danger">{errors.content.message}</p>}
        </div>
        <Button type="submit" loading={loading}>Create Thread</Button>
      </form>
    </div>
  );
}
