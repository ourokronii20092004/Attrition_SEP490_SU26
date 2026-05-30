"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { wikiApi } from "@/lib/api/wiki";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import type { WikiArticleDto } from "@/lib/types";

const schema = z.object({
  suggestedContent: z.string().min(10, "Content too short"),
  changeNote: z.string().min(3, "Provide a summary"),
});

type FormData = z.infer<typeof schema>;

export default function SuggestEditPage() {
  const params = useParams<{ slug: string }>();
  const { user } = useAuth();
  const router = useRouter();
  const [article, setArticle] = useState<WikiArticleDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  useEffect(() => {
    if (!params.slug) return;
    wikiApi.getArticle(params.slug).then((res) => {
      if (res.success && res.data) {
        setArticle(res.data);
        setValue("suggestedContent", res.data.content);
      }
      setLoading(false);
    });
  }, [params.slug, setValue]);

  if (!user) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <p className="text-fg-muted">You must be signed in to suggest edits.</p>
        <Link href="/login" className="mt-4 inline-block text-accent hover:underline">Sign in</Link>
      </div>
    );
  }

  if (loading) return <PageLoader />;
  if (!article) return null;

  const onSubmit = async (data: FormData) => {
    setError("");
    setSubmitting(true);
    try {
      await wikiApi.suggestEdit(article.id, data);
      router.push(`/wiki/${params.slug}`);
    } catch {
      setError("Failed to submit suggestion");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href={`/wiki/${params.slug}`} className="text-sm text-accent hover:underline">&larr; Back to article</Link>
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">Suggest Edit</h1>
      <p className="mt-1 text-fg-muted">Editing: {article.title}</p>

      {error && <div className="mt-4 rounded-lg bg-danger/10 px-4 py-3 text-sm text-danger">{error}</div>}

      <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
        <div className="space-y-1">
          <label className="block text-sm font-medium text-fg-muted">Content (Markdown)</label>
          <textarea
            {...register("suggestedContent")}
            rows={20}
            className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 font-mono text-sm text-fg outline-none focus:border-accent"
          />
          {errors.suggestedContent && <p className="text-xs text-danger">{errors.suggestedContent.message}</p>}
        </div>
        <Input label="Change Note" {...register("changeNote")} error={errors.changeNote?.message} placeholder="Briefly describe your changes" />
        <Button type="submit" loading={submitting}>Submit Suggestion</Button>
      </form>
    </div>
  );
}
