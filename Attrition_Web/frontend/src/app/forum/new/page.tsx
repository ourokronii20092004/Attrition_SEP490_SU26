"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { forumApi } from "@/lib/api/forum";
import { useAuth } from "@/lib/providers";
import { ArrowLeft } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { EmptyState } from "@/components/ui/empty-state";
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
      <PageShell size="md">
        <EmptyState
          title="Sign in required"
          description="You must be signed in to create a thread."
          action={<Link href="/login"><Button variant="secondary">Sign in</Button></Link>}
        />
      </PageShell>
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
            <label className="block text-sm font-medium text-fg-muted">Content</label>
            <textarea
              {...register("content")}
              rows={10}
              className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
            />
            {errors.content && <p className="text-xs text-danger">{errors.content.message}</p>}
          </div>
          <Button type="submit" loading={loading}>Create Thread</Button>
        </form>
      </Card>
    </PageShell>
  );
}
