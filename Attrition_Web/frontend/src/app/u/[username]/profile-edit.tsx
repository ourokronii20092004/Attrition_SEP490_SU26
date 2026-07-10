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

/**
 * Immersive cover banner. Always renders (a procedural corruption-haze fills the
 * frame when no cover is set) so every profile gets a consistent hero. Owners get
 * overlaid upload/remove controls. Layout (full-bleed, rounding) is owned by the page.
 */
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

  return (
    <div className="group relative h-60 w-full overflow-hidden rounded-3xl border border-border bg-surface-2 sm:h-80">
      {bg ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={bg} alt="" className="h-full w-full object-cover" />
      ) : (
        // No cover set: a layered corruption-haze frame rather than dead space.
        <div className="absolute inset-0">
          <div className="absolute inset-0 bg-gradient-to-br from-surface-2 via-surface to-accent-soft/40" />
          <span aria-hidden className="absolute -right-16 -top-20 h-72 w-72 rounded-full bg-accent/15 blur-[90px]" />
          <span aria-hidden className="absolute -bottom-24 left-1/4 h-64 w-64 rounded-full bg-info/10 blur-[90px]" />
        </div>
      )}

      {/* Top hairline + bottom scrim keep the straddling avatar and page seam legible. */}
      <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />
      <div aria-hidden className="pointer-events-none absolute inset-x-0 bottom-0 h-28 bg-gradient-to-t from-bg/90 to-transparent" />

      {/* Archive-scan registration marks — thin corner brackets that frame the record. */}
      <span aria-hidden className="pointer-events-none absolute left-3 top-3 h-5 w-5 rounded-tl-lg border-l border-t border-accent/25 sm:left-4 sm:top-4" />
      <span aria-hidden className="pointer-events-none absolute bottom-3 right-3 h-5 w-5 rounded-br-lg border-b border-r border-accent/25 sm:bottom-4 sm:right-4" />

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

/** Avatar with an owner-only camera button to upload a new one. Positioning is owned by the page. */
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
    <span className="relative inline-block">
      <Avatar
        src={profile.avatarUrl}
        name={profile.displayName ?? profile.username}
        size="2xl"
        className="ring-4 ring-bg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_25%,transparent),var(--shadow-md)]"
      />
      {isOwner && (
        <>
          <button
            onClick={() => inputRef.current?.click()}
            disabled={busy}
            className="absolute bottom-1 right-1 inline-flex h-9 w-9 items-center justify-center rounded-full bg-accent text-accent-fg shadow-md transition-transform hover:scale-105 disabled:opacity-50"
            aria-label="Change avatar"
          >
            <Camera size={16} />
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
      <div className="flex items-center justify-center gap-2 sm:justify-start">
        <input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={50}
          autoFocus
          className="w-64 max-w-full rounded-lg border border-border bg-surface-2 px-2.5 py-1 font-display text-3xl font-bold text-fg outline-none focus:border-accent"
        />
        <button onClick={save} disabled={busy} className="text-success transition-opacity hover:opacity-80 disabled:opacity-50" aria-label="Save name"><Check size={22} /></button>
        <button onClick={() => { setEditing(false); setValue(profile.displayName ?? ""); }} className="text-fg-muted transition-opacity hover:opacity-80" aria-label="Cancel"><X size={22} /></button>
      </div>
    );
  }

  return (
    <h1 className="group flex items-center justify-center gap-2 font-display text-3xl font-bold tracking-tight text-fg sm:justify-start sm:text-4xl">
      <span className="break-words">{profile.displayName ?? profile.username}</span>
      {isOwner && (
        <button onClick={() => setEditing(true)} className="shrink-0 text-fg-subtle opacity-0 transition-opacity hover:text-accent group-hover:opacity-100" aria-label="Edit name">
          <Pencil size={18} />
        </button>
      )}
    </h1>
  );
}

/**
 * Bio, rendered as an archival "field note" block. Public viewers see the text (or,
 * when empty, nothing); owners get an inline textarea editor and an add-bio prompt.
 */
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
      <div>
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
      <button onClick={() => setEditing(true)} className="inline-flex items-center gap-1.5 rounded-lg border border-dashed border-border px-3 py-2 text-sm text-fg-subtle transition-colors hover:border-accent/50 hover:text-fg">
        <Pencil size={14} /> Add a bio
      </button>
    );
  }

  return (
    <div className="group relative">
      <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-fg-muted">{profile.bio}</p>
      {isOwner && (
        <button onClick={() => setEditing(true)} className="mt-3 inline-flex items-center gap-1.5 text-xs text-fg-subtle transition-colors hover:text-accent" aria-label="Edit bio">
          <Pencil size={13} /> Edit
        </button>
      )}
    </div>
  );
}
