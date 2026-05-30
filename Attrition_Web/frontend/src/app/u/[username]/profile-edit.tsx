"use client";

import { useRef, useState } from "react";
import { Camera, Pencil, Check, X, ImagePlus, Trash2 } from "lucide-react";
import { accountApi } from "@/lib/api/account";
import { resolveMediaUrl } from "@/lib/api/media";
import { Avatar } from "@/components/ui/avatar";
import type { UserDto } from "@/lib/types";

interface EditProps {
  profile: UserDto;
  isOwner: boolean;
  onEdited: () => void | Promise<void>;
}

/** Background banner with owner upload/remove controls overlaid. */
export function ProfileBanner({ profile, isOwner, onEdited }: EditProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const bg = resolveMediaUrl(profile.backgroundUrl);

  const upload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    try { await accountApi.uploadBackground(file); await onEdited(); } finally { setBusy(false); }
  };

  const remove = async () => {
    setBusy(true);
    try { await accountApi.deleteBackground(); await onEdited(); } finally { setBusy(false); }
  };

  if (!bg && !isOwner) return null;

  return (
    <div className="relative -mx-4 -mt-8 mb-6 h-48 overflow-hidden bg-surface-2 sm:rounded-b-2xl">
      {bg && <img src={bg} alt="" className="h-full w-full object-cover" />}
      <div className="absolute inset-0 bg-gradient-to-t from-bg to-transparent" />
      {isOwner && (
        <div className="absolute right-3 top-3 flex gap-2">
          <button
            onClick={() => inputRef.current?.click()}
            disabled={busy}
            className="glass inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium text-fg shadow-sm transition-colors hover:text-accent disabled:opacity-50"
          >
            <ImagePlus size={14} /> {busy ? "Saving..." : bg ? "Change cover" : "Add cover"}
          </button>
          {bg && (
            <button
              onClick={remove}
              disabled={busy}
              className="glass inline-flex items-center rounded-lg px-2.5 py-1.5 text-xs text-danger shadow-sm transition-opacity hover:opacity-80 disabled:opacity-50"
              aria-label="Remove cover"
            >
              <Trash2 size={14} />
            </button>
          )}
          <input ref={inputRef} type="file" accept="image/*" onChange={upload} className="hidden" />
        </div>
      )}
    </div>
  );
}

/** Avatar with an owner-only camera button to upload a new one. */
export function ProfileAvatar({ profile, isOwner, onEdited }: EditProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const upload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    try { await accountApi.uploadAvatar(file); await onEdited(); } finally { setBusy(false); }
  };

  return (
    <span className="relative -mt-12 inline-block rounded-full ring-4 ring-bg">
      <Avatar src={profile.avatarUrl} name={profile.displayName ?? profile.username} size="xl" />
      {isOwner && (
        <>
          <button
            onClick={() => inputRef.current?.click()}
            disabled={busy}
            className="absolute bottom-0 right-0 inline-flex h-8 w-8 items-center justify-center rounded-full bg-accent text-accent-fg shadow-md transition-transform hover:scale-105 disabled:opacity-50"
            aria-label="Change avatar"
          >
            <Camera size={15} />
          </button>
          <input ref={inputRef} type="file" accept="image/*" onChange={upload} className="hidden" />
        </>
      )}
    </span>
  );
}

/** Display name with inline owner editing (pencil → input → save/cancel). */
export function ProfileName({ profile, isOwner, onEdited }: EditProps) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(profile.displayName ?? "");
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    try {
      await accountApi.updateProfile({ displayName: value.trim() });
      setEditing(false);
      await onEdited();
    } finally { setBusy(false); }
  };

  if (editing) {
    return (
      <div className="flex items-center gap-2">
        <input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={50}
          autoFocus
          className="w-56 rounded-lg border border-border bg-surface-2 px-2.5 py-1 font-display text-2xl font-bold text-fg outline-none focus:border-accent"
        />
        <button onClick={save} disabled={busy} className="text-success transition-opacity hover:opacity-80 disabled:opacity-50" aria-label="Save name"><Check size={20} /></button>
        <button onClick={() => { setEditing(false); setValue(profile.displayName ?? ""); }} className="text-fg-muted transition-opacity hover:opacity-80" aria-label="Cancel"><X size={20} /></button>
      </div>
    );
  }

  return (
    <h1 className="group flex items-center gap-2 font-display text-3xl font-bold tracking-tight text-fg">
      {profile.displayName ?? profile.username}
      {isOwner && (
        <button onClick={() => setEditing(true)} className="text-fg-subtle opacity-0 transition-opacity hover:text-accent group-hover:opacity-100" aria-label="Edit name">
          <Pencil size={16} />
        </button>
      )}
    </h1>
  );
}
