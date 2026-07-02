"use client";

import { useRef, useState } from "react";
import { Camera, Pencil, Check, X, ImagePlus, Trash2 } from "lucide-react";
import { accountApi } from "@/lib/api/account";
import { resolveMediaUrl } from "@/lib/api/media";
import { useToast } from "@/lib/providers";
import { Avatar } from "@/components/ui/avatar";
import { ImageCropper } from "@/components/image-cropper";
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
  const [cropFile, setCropFile] = useState<File | null>(null);
  const { toast } = useToast();
  const bg = resolveMediaUrl(profile.backgroundUrl);

  const pick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file) setCropFile(file);
  };

  const upload = async (file: File) => {
    setCropFile(null);
    setBusy(true);
    try { await accountApi.uploadBackground(file); await onEdited(); toast("Cover updated.", "success"); }
    catch { toast("Failed to upload cover. Please try again.", "error"); }
    finally { setBusy(false); }
  };

  const remove = async () => {
    setBusy(true);
    try { await accountApi.deleteBackground(); await onEdited(); toast("Cover removed.", "success"); }
    catch { toast("Failed to remove cover. Please try again.", "error"); }
    finally { setBusy(false); }
  };

  if (!bg && !isOwner) return null;

  return (
    <div className="relative -mx-4 -mt-8 h-56 overflow-hidden bg-surface-2 sm:-mx-8 sm:h-64 sm:rounded-b-2xl">
      {bg ? (
        <img src={bg} alt="" className="h-full w-full object-cover" />
      ) : (
        // No cover set (owner view): a subtle corruption-tinted placeholder instead of dead space.
        <div className="h-full w-full bg-gradient-to-br from-surface-2 via-surface to-accent-soft/40" />
      )}
      {/* Bottom-only scrim so the avatar/name below stay legible without washing out the image. */}
      <div className="absolute inset-x-0 bottom-0 h-24 bg-gradient-to-t from-bg to-transparent" />
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
          <input ref={inputRef} type="file" accept="image/*" onChange={pick} className="hidden" />
        </div>
      )}
      {cropFile && (
        <ImageCropper file={cropFile} aspect={16 / 6} onCancel={() => setCropFile(null)} onCropped={upload} />
      )}
    </div>
  );
}

/** Avatar with an owner-only camera button to upload a new one. */
export function ProfileAvatar({ profile, isOwner, onEdited }: EditProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [cropFile, setCropFile] = useState<File | null>(null);
  const { toast } = useToast();

  const pick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file) setCropFile(file);
  };

  const upload = async (file: File) => {
    setCropFile(null);
    setBusy(true);
    try { await accountApi.uploadAvatar(file); await onEdited(); toast("Avatar updated.", "success"); }
    catch { toast("Failed to upload avatar. Please try again.", "error"); }
    finally { setBusy(false); }
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
          <input ref={inputRef} type="file" accept="image/*" onChange={pick} className="hidden" />
        </>
      )}
      {cropFile && (
        <ImageCropper file={cropFile} aspect={1} round onCancel={() => setCropFile(null)} onCropped={upload} />
      )}
    </span>
  );
}

/** Display name with inline owner editing (pencil → input → save/cancel). */
export function ProfileName({ profile, isOwner, onEdited }: EditProps) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(profile.displayName ?? "");
  const [busy, setBusy] = useState(false);
  const { toast } = useToast();

  const save = async () => {
    setBusy(true);
    try {
      await accountApi.updateProfile({ displayName: value.trim() });
      setEditing(false);
      await onEdited();
      toast("Name updated.", "success");
    } catch {
      toast("Failed to update name. Please try again.", "error");
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

/** Bio with inline owner editing. Public viewers see the text (or nothing); owners get a textarea. */
export function ProfileBio({ profile, isOwner, onEdited }: EditProps) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(profile.bio ?? "");
  const [busy, setBusy] = useState(false);
  const { toast } = useToast();

  const save = async () => {
    setBusy(true);
    try {
      await accountApi.updateProfile({ bio: value.trim() });
      setEditing(false);
      await onEdited();
      toast("Bio updated.", "success");
    } catch {
      toast("Failed to update bio. Please try again.", "error");
    } finally { setBusy(false); }
  };

  if (editing) {
    return (
      <div className="mt-6">
        <textarea
          value={value}
          onChange={(e) => setValue(e.target.value)}
          rows={4}
          maxLength={500}
          autoFocus
          placeholder="Tell the archive who you are…"
          className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm leading-relaxed text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
        />
        <div className="mt-2 flex items-center gap-2">
          <button onClick={save} disabled={busy} className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-1.5 text-xs font-semibold text-accent-fg transition-[filter] hover:brightness-105 disabled:opacity-50">
            <Check size={14} /> {busy ? "Saving…" : "Save"}
          </button>
          <button onClick={() => { setEditing(false); setValue(profile.bio ?? ""); }} className="rounded-md border border-border px-3 py-1.5 text-xs font-medium text-fg-muted transition-colors hover:text-fg">
            Cancel
          </button>
          <span className="ml-auto text-[11px] tabular-nums text-fg-subtle">{value.length}/500</span>
        </div>
      </div>
    );
  }

  if (!profile.bio) {
    if (!isOwner) return null;
    return (
      <button onClick={() => setEditing(true)} className="mt-6 inline-flex items-center gap-1.5 rounded-lg border border-dashed border-border px-3 py-2 text-sm text-fg-subtle transition-colors hover:border-accent/50 hover:text-fg">
        <Pencil size={14} /> Add a bio
      </button>
    );
  }

  return (
    <div className="group relative mt-6">
      <p className="whitespace-pre-wrap leading-relaxed text-fg-muted">{profile.bio}</p>
      {isOwner && (
        <button onClick={() => setEditing(true)} className="absolute -right-1 -top-1 text-fg-subtle opacity-0 transition-opacity hover:text-accent group-hover:opacity-100" aria-label="Edit bio">
          <Pencil size={15} />
        </button>
      )}
    </div>
  );
}
