"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { assetsApi } from "@/lib/api/assets";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import type { AssetDto, UpdateAssetReq } from "@/lib/types";
import { ASSET_TYPES } from "./UploadForm";

const editAssetSchema = z.object({
  title: z.string(),
  description: z.string(),
  assetType: z.string(),
  tags: z.string(),
});

type EditAssetFormValues = z.infer<typeof editAssetSchema>;

export function EditForm({ asset, onDone, onCancel }: { asset: AssetDto; onDone: () => void; onCancel: () => void }) {
  const [error, setError] = useState<string | null>(null);

  const {
    register, handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<EditAssetFormValues>({
    resolver: zodResolver(editAssetSchema),
    defaultValues: {
      title: asset.title ?? "",
      description: asset.description ?? "",
      assetType: asset.assetType,
      tags: asset.tags ?? "",
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    const data: UpdateAssetReq = {
      title: values.title || undefined,
      description: values.description || undefined,
      assetType: values.assetType || undefined,
      tags: values.tags || undefined,
    };
    try {
      await assetsApi.update(asset.id, data);
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to save changes. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="mt-4 card p-4 space-y-3">
      <h3 className="text-sm font-medium text-fg">Edit: {asset.fileName}</h3>
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" error={errors.title?.message} {...register("title")} />
      <Select label="Asset Type" {...register("assetType")}>
        {ASSET_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
      </Select>
      <Input label="Description" {...register("description")} />
      <Input label="Tags (comma-separated)" {...register("tags")} />
      <div className="flex gap-2">
        <Button type="submit" loading={isSubmitting}>Save</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
