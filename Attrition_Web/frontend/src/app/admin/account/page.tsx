"use client";

import { useState } from "react";
import { useAuth, useToast, useTheme, ACCENTS } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { authApi } from "@/lib/api/auth";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Toggle } from "@/components/ui/toggle";
import { SaveStatus } from "@/components/account-sections";
import { useAutoSave } from "@/lib/hooks/use-auto-save";
import type { UpdateProfileRequest } from "@/lib/types";
import { ImageCropper } from "@/components/image-cropper";
import { PageLoader } from "@/components/ui/spinner";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { ProfileName } from "@/app/u/[username]/profile-edit";
import { PrivacySection, ConnectionsSection, DangerSection } from "@/components/account-sections";
import { Sun, Moon } from "lucide-react";

/** Compact panel used across the admin account grid — tighter than the user SettingsCard. */
function Panel({ title, children, className }: { title: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={`rounded-lg border border-border bg-surface/50 p-4 ${className ?? ""}`}>
      <h2 className="text-xs font-semibold uppercase tracking-wider text-fg-subtle">{title}</h2>
      <div className="mt-3">{children}</div>
    </section>
  );
}

export default function AdminAccountPage() {
  const { user, loading, refreshUser, setUser } = useAuth();
  const { toast } = useToast();
  const { mode, accent, setTheme } = useTheme();

  // Profile
  const [bio, setBio] = useState("");
  const [notifyReply, setNotifyReply] = useState(true);
  const [notifyMention, setNotifyMention] = useState(true);
  const [profileInit, setProfileInit] = useState(false);

  // Avatar
  const [cropFile, setCropFile] = useState<File | null>(null);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  // Cover image
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [uploadingCover, setUploadingCover] = useState(false);

  // Password
  const [curPw, setCurPw] = useState("");
  const [newPw, setNewPw] = useState("");
  const [confirmPw, setConfirmPw] = useState("");
  const [savingPw, setSavingPw] = useState(false);

  // Email
  const [email, setEmail] = useState("");
  const [emailPw, setEmailPw] = useState("");
  const [savingEmail, setSavingEmail] = useState(false);

  if (loading) return <PageLoader />;
  if (!user) return null;

  // Seed profile fields once the user is available.
  if (!profileInit) {
    setBio(user.bio ?? "");
    setNotifyReply(user.notifyOnReply ?? true);
    setNotifyMention(user.notifyOnMention ?? true);
    setProfileInit(true);
  }

  // Saves as each control changes, so nothing is lost by navigating away mid-edit. Patches carry
  // only the field that changed; the API leaves omitted fields alone.
  const { save: saveProfile, state: profileSaveState } = useAutoSave<UpdateProfileRequest>({
    onSave: async (patch) => {
      const res = await accountApi.updateProfile(patch);
      if (res.success && res.data) setUser(res.data);
      else throw new Error(res.error ?? "save failed");
    },
    onError: (message) => toast(message, "error"),
  });

  const onCropped = async (file: File) => {
    setCropFile(null);
    setUploadingAvatar(true);
    try { await accountApi.uploadAvatar(file); await refreshUser(); toast("Avatar updated.", "success"); }
    catch { toast("Failed to upload avatar.", "error"); }
    setUploadingAvatar(false);
  };

  const removeAvatar = async () => {
    try { await accountApi.deleteAvatar(); await refreshUser(); toast("Avatar removed.", "success"); }
    catch { toast("Failed to remove avatar.", "error"); }
  };

  const onCoverCropped = async (file: File) => {
    setCoverFile(null);
    setUploadingCover(true);
    try { await accountApi.uploadBackground(file); await refreshUser(); toast("Cover updated.", "success"); }
    catch { toast("Failed to upload cover.", "error"); }
    setUploadingCover(false);
  };

  const removeCover = async () => {
    try { await accountApi.deleteBackground(); await refreshUser(); toast("Cover removed.", "success"); }
    catch { toast("Failed to remove cover.", "error"); }
  };

  const savePassword = async () => {
    if (newPw !== confirmPw) { toast("The new passwords don't match.", "error"); return; }
    setSavingPw(true);
    try {
      await authApi.changePassword({ currentPassword: curPw, newPassword: newPw });
      setCurPw(""); setNewPw(""); setConfirmPw("");
      toast("Password updated. A confirmation email has been sent.", "success");
    } catch (err) { toast(parseApiError(err, "Failed to update password."), "error"); }
    setSavingPw(false);
  };

  const saveEmail = async () => {
    setSavingEmail(true);
    try {
      await accountApi.updateEmail({ newEmail: email, currentPassword: emailPw });
      setEmail(""); setEmailPw("");
      await refreshUser();
      toast("Verification email sent to new address.", "success");
    } catch { toast("Failed to update email.", "error"); }
    setSavingEmail(false);
  };

  const coverUrl = resolveMediaUrl(user.backgroundUrl);

  return (
    <div>
      {/* Identity header — avatar + name (inline-editable) + avatar controls, on one line */}
      <div className="flex items-center gap-4 rounded-lg border border-border bg-surface/50 p-4">
        <Avatar src={user.avatarUrl} name={user.displayName ?? user.username} size="lg" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            {/* Reuse the public profile's inline name editor: admins can't reach /u/[username]
                (AppFrame bounces them), so this is the only place they edit their display name. */}
            <ProfileName profile={user} isOwner onEdited={refreshUser} />
            <span className="rounded-full bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent">Admin</span>
          </div>
          <p className="text-sm text-fg-muted">@{user.username}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <label className="cursor-pointer rounded-md border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
            {uploadingAvatar ? "Uploading…" : "Change avatar"}
            <input type="file" accept="image/*" className="hidden"
              onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ""; if (f) setCropFile(f); }} />
          </label>
          {user.avatarUrl && <button onClick={removeAvatar} className="text-sm text-danger transition-opacity hover:opacity-80">Remove</button>}
        </div>
      </div>

      {/* Cover image — the admin's public-profile banner. (Regular users set this on their profile
          page; admins get it here since they work out of the dashboard.) */}
      <div className="mt-4 rounded-lg border border-border bg-surface/50 p-4">
        <h2 className="text-xs font-semibold uppercase tracking-wider text-fg-subtle">Cover image</h2>
        <div className="relative mt-3 aspect-[16/6] w-full overflow-hidden rounded-lg border border-border bg-surface-2">
          {coverUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={coverUrl} alt="" className="h-full w-full object-cover" />
          ) : (
            <div className="absolute inset-0 bg-gradient-to-br from-surface-2 via-surface to-accent-soft/40" />
          )}
          <div className="absolute right-3 top-3 flex gap-2">
            <label className="glass inline-flex cursor-pointer items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium text-fg shadow-sm transition-colors hover:text-accent">
              {uploadingCover ? "Saving…" : coverUrl ? "Change cover" : "Add cover"}
              <input type="file" accept="image/*" className="hidden" disabled={uploadingCover}
                onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ""; if (f) setCoverFile(f); }} />
            </label>
            {coverUrl && (
              <button onClick={removeCover} className="glass inline-flex items-center rounded-lg px-2.5 py-1.5 text-xs text-danger shadow-sm transition-opacity hover:opacity-80">Remove</button>
            )}
          </div>
        </div>
        <p className="mt-2 text-xs text-fg-subtle">Displayed on your public profile. A wide (16:6) image works best.</p>
      </div>

      {/* Dense 2-column grid — everything visible without scrolling on a normal screen */}
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <Panel title="Profile">
          <div className="space-y-3">
            <textarea
              value={bio}
              onChange={(e) => setBio(e.target.value)}
              onBlur={() => { if (bio !== (user.bio ?? "")) saveProfile({ bio }); }}
              rows={3}
              placeholder="Bio"
              aria-label="Bio"
              className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
            />
            <Toggle
              checked={notifyReply}
              onChange={(next) => { setNotifyReply(next); saveProfile({ notifyOnReply: next }, () => setNotifyReply(!next)); }}
              label="Notify on replies"
            />
            <Toggle
              checked={notifyMention}
              onChange={(next) => { setNotifyMention(next); saveProfile({ notifyOnMention: next }, () => setNotifyMention(!next)); }}
              label="Notify on mentions"
            />
            <div className="flex items-center justify-between">
              <span className="text-xs text-fg-subtle">Changes save automatically.</span>
              <SaveStatus state={profileSaveState} />
            </div>
          </div>
        </Panel>

        <Panel title="Theme">
          <div className="space-y-4">
            <div className="grid max-w-xs grid-cols-2 gap-2">
              <button onClick={() => setTheme({ mode: "dark" })}
                className={`flex items-center justify-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${mode === "dark" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}>
                <Moon size={15} /> Dark
              </button>
              <button onClick={() => setTheme({ mode: "light" })}
                className={`flex items-center justify-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${mode === "light" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}>
                <Sun size={15} /> Light
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              {ACCENTS.map((a) => (
                <button key={a.name} onClick={() => setTheme({ accent: a.name })} title={a.name} aria-label={a.name} aria-pressed={accent === a.name}
                  style={{ backgroundColor: a.color }}
                  className={`h-8 w-8 rounded-full transition-transform hover:scale-110 ${accent === a.name ? "ring-2 ring-fg ring-offset-2 ring-offset-surface" : ""}`} />
              ))}
            </div>
            <p className="text-xs text-fg-subtle">Theme changes apply and save instantly.</p>
          </div>
        </Panel>

        <Panel title="Password">
          <div className="space-y-3">
            <Input type="password" autoComplete="current-password" placeholder="Current password" value={curPw} onChange={(e) => setCurPw(e.target.value)} />
            <Input type="password" autoComplete="new-password" placeholder="New password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
            <Input type="password" autoComplete="new-password" placeholder="Repeat new password" value={confirmPw} onChange={(e) => setConfirmPw(e.target.value)} />
            <p className="text-xs text-fg-subtle">A confirmation email is sent after the change.</p>
            <Button size="sm" onClick={savePassword} loading={savingPw} disabled={!curPw || !newPw || !confirmPw}>Change Password</Button>
          </div>
        </Panel>

        <Panel title="Email">
          <p className="text-sm text-fg-muted">
            Current: {user.email ?? "none"}{" "}
            {user.email && (user.isEmailVerified
              ? <span className="text-success">(verified)</span>
              : <span className="text-warning">(unverified)</span>)}
          </p>
          <div className="mt-3 space-y-3">
            <Input type="email" autoComplete="email" placeholder="New email" value={email} onChange={(e) => setEmail(e.target.value)} />
            <Input type="password" autoComplete="current-password" placeholder="Current password" value={emailPw} onChange={(e) => setEmailPw(e.target.value)} />
            <Button size="sm" onClick={saveEmail} loading={savingEmail} disabled={!email || !emailPw}>Update Email</Button>
          </div>
        </Panel>
      </div>

      {/* Newest account features — reuse the user-settings sections so admin and user stay in
          lockstep. They carry their own card styling; the grid keeps the two-column rhythm. */}
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <PrivacySection user={user} setUser={setUser} />
        <ConnectionsSection />
      </div>
      <div className="mt-4">
        <DangerSection logout={() => {}} />
      </div>

      {cropFile && (
        <ImageCropper file={cropFile} aspect={1} round onCancel={() => setCropFile(null)} onCropped={onCropped} />
      )}
      {coverFile && (
        <ImageCropper file={coverFile} aspect={16 / 6} onCancel={() => setCoverFile(null)} onCropped={onCoverCropped} />
      )}
    </div>
  );
}
