"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { useToast } from "@/lib/providers";
import { Avatar } from "@/components/ui/avatar";
import { ImageCropper } from "@/components/image-cropper";
import type { UserDto } from "@/lib/types";
import { SettingsCard } from "./SettingsCard";

export function AvatarSection({ user, refreshUser }: { user: UserDto; refreshUser: () => Promise<void> }) {
  const { toast } = useToast();
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [cropFile, setCropFile] = useState<File | null>(null);

  // Pick a file → open the cropper. Upload happens after the user frames it (QOLF-3).
  const handlePick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file) { setError(""); setCropFile(file); }
  };

  const handleCropped = async (file: File) => {
    setCropFile(null);
    setUploading(true);
    setError("");
    try { await accountApi.uploadAvatar(file); await refreshUser(); toast("Avatar updated.", "success"); }
    catch { setError("Failed to upload avatar. Please try again."); toast("Failed to upload avatar. Please try again.", "error"); }
    setUploading(false);
  };

  const handleDelete = async () => {
    setError("");
    try { await accountApi.deleteAvatar(); await refreshUser(); toast("Avatar removed.", "success"); }
    catch { setError("Failed to remove avatar. Please try again."); toast("Failed to remove avatar. Please try again.", "error"); }
  };

  return (
    <SettingsCard title="Avatar">
      <div className="flex items-center gap-4">
        <Avatar src={user.avatarUrl} name={user.displayName ?? user.username} size="lg" />
        <div className="flex flex-wrap items-center gap-2">
          <label className="cursor-pointer rounded-lg border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
            {uploading ? "Uploading..." : "Upload Avatar"}
            <input type="file" accept="image/*" onChange={handlePick} className="hidden" />
          </label>
          {user.avatarUrl && (
            <button onClick={handleDelete} className="text-sm text-danger transition-opacity hover:opacity-80">Remove</button>
          )}
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      {cropFile && (
        <ImageCropper file={cropFile} aspect={1} round onCancel={() => setCropFile(null)} onCropped={handleCropped} />
      )}
    </SettingsCard>
  );
}
