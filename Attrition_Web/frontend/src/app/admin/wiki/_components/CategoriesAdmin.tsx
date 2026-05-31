"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { wikiApi } from "@/lib/api/wiki";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { qk } from "@/lib/query-keys";

const categorySchema = z.object({
  name: z.string().min(1, "Required"),
  description: z.string(),
});

type CategoryFormValues = z.infer<typeof categorySchema>;

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [error, setError] = useState<string | null>(null);

  const {
    register, handleSubmit, reset, watch,
    formState: { errors, isSubmitting },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: "", description: "" },
  });

  const { data: categories = [], isPending: loading } = useQuery({
    queryKey: qk.wiki.categories(),
    queryFn: async () => {
      const res = await wikiApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.wiki.categories() });

  const removeMutation = useMutation({
    mutationFn: async (id: number) => { await wikiApi.deleteCategory(id); },
    onSuccess: invalidate,
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await wikiApi.createCategory({ name: values.name, description: values.description });
      reset();
      invalidate();
    } catch (err) {
      setError(parseApiError(err, "Failed to create the category. Please try again."));
    }
  });

  const remove = async (id: number) => {
    if (!(await confirm({ message: "Delete this category?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  if (loading) return <PageLoader />;

  return (
    <div>
      <form onSubmit={onSubmit} className="card mb-6 space-y-3 p-4">
        {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
        <Input label="New category name" error={errors.name?.message} {...register("name")} />
        <Input label="Description" {...register("description")} />
        <Button type="submit" loading={isSubmitting} disabled={!watch("name").trim()}>Add Category</Button>
      </form>
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
