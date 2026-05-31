"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { parseApiError } from "@/lib/api/parse-error";
import { useConfirm, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { qk } from "@/lib/query-keys";

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const { data: categories = [], isPending: loading } = useQuery({
    queryKey: qk.forum.categories(),
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: async () => { await forumApi.createCategory({ name, description }); },
    onSuccess: () => {
      setName(""); setDescription("");
      queryClient.invalidateQueries({ queryKey: qk.forum.categories() });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: number) => { await forumApi.deleteCategory(id); },
    onSuccess: () => {
      toast("Category deleted.", "success");
      queryClient.invalidateQueries({ queryKey: qk.forum.categories() });
    },
    onError: (err) => {
      // Surfaces the backend's "still has threads" 409 message clearly.
      toast(parseApiError(err, "Could not delete the category."), "error");
    },
  });

  const create = () => {
    if (!name.trim()) return;
    createMutation.mutate();
  };

  const remove = async (id: number, label: string) => {
    if (!(await confirm({ message: `Delete the "${label}" category?`, danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
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
            <Link href={`/admin/forum/categories/${c.id}`} className="min-w-0 flex-1 transition-colors hover:text-accent">
              <p className="font-medium text-fg">{c.name}</p>
              <p className="text-xs text-fg-muted">{c.threadCount} threads · {c.slug}</p>
            </Link>
            <Button
              size="sm"
              variant="danger"
              loading={deleteMutation.isPending && deleteMutation.variables === c.id}
              onClick={() => remove(c.id, c.name)}
            >
              Delete
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}
