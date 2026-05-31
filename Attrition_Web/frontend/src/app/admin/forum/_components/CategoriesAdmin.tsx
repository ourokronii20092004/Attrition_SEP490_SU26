"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { qk } from "@/lib/query-keys";

export function CategoriesAdmin() {
  const queryClient = useQueryClient();
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

  const create = () => {
    if (!name.trim()) return;
    createMutation.mutate();
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
              <p className="text-xs text-fg-muted">{c.threadCount} threads · {c.slug}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
