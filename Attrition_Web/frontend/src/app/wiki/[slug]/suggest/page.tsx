"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { wikiApi } from "@/lib/api/wiki";
import { useAuth } from "@/lib/providers";
import { ArrowLeft } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";

const schema = z.object({
  suggestedContent: z.string().min(10, "Content too short"),
  changeNote: z.string().min(3, "Provide a summary"),
});

type FormData = z.infer<typeof schema>;

export default function SuggestEditPage() {
  const params = useParams<{ slug: string }>();
  const { user } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const { data: article, isPending } = useQuery({
    queryKey: qk.wiki.article(params.slug),
    enabled: !!params.slug,
    queryFn: async () => {
      const res = await wikiApi.getArticle(params.slug);
      return res.success ? res.data : null;
    },
  });

  useEffect(() => {
    if (article) setValue("suggestedContent", article.content);
  }, [article, setValue]);

  const submitMutation = useMutation({
    mutationFn: async (data: FormData) => {
      if (!article) throw new Error("Article not loaded");
      await wikiApi.suggestEdit(article.id, data);
    },
    onSuccess: () => {
      router.push(`/wiki/${params.slug}`);
    },
    onError: () => {
      setError("Failed to submit suggestion");
    },
  });

  if (!user) {
    return (
      <PageShell size="md">
        <EmptyState
          title="Sign in required"
          description="You must be signed in to suggest edits."
          action={<Link href="/login"><Button variant="secondary">Sign in</Button></Link>}
        />
      </PageShell>
    );
  }

  if (isPending) {
    return (
      <PageShell size="md">
        <Skeleton className="h-4 w-28" />
        <Skeleton className="mt-4 h-9 w-1/2" />
        <Skeleton className="mt-6 h-80 w-full rounded-card" />
      </PageShell>
    );
  }
  if (!article) return null;

  const onSubmit = (data: FormData) => {
    setError("");
    submitMutation.mutate(data);
  };

  return (
    <PageShell size="md">
      <Link href={`/wiki/${params.slug}`} className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Back to article
      </Link>
      <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Suggest Edit</h1>
      <p className="mt-1 text-fg-muted">Editing: {article.title}</p>

      {error && (
        <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
          {error}
        </div>
      )}

      <Card className="mt-6 p-5 sm:p-6">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-fg-muted">Content (Markdown)</label>
            <textarea
              {...register("suggestedContent")}
              rows={20}
              className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 font-mono text-sm text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
            />
            {errors.suggestedContent && <p className="text-xs text-danger">{errors.suggestedContent.message}</p>}
          </div>
          <Input label="Change Note" {...register("changeNote")} error={errors.changeNote?.message} placeholder="Briefly describe your changes" />
          <Button type="submit" loading={submitMutation.isPending}>Submit Suggestion</Button>
        </form>
      </Card>
    </PageShell>
  );
}
