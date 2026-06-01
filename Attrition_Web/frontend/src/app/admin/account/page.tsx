"use client";

import { useState } from "react";
import { useAuth, useToast, useTheme, ACCENTS } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { authApi } from "@/lib/api/auth";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Toggle } from "@/components/ui/toggle";
import { ImageCropper } from "@/components/image-cropper";
import { PageLoader } from "@/components/ui/spinner";
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
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileInit, setProfileInit] = useState(false);

  // Avatar
  const [cropFile, setCropFile] = useState<File | null>(null);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  // Password
  const [curPw, setCurPw] = useState("");
  const [newPw, setNewPw] = useState("");
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

  const saveProfile = async () => {
    setSavingProfile(true);
    try {
      const res = await accountApi.updateProfile({ bio, notifyOnReply: notifyReply, notifyOnMention: notifyMention });
      if (res.success && res.data) setUser(res.data);
      toast("Profile saved.", "success");
    } catch { toast("Failed to save profile.", "error"); }
    setSavingProfile(false);
  };

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

  const savePassword = async () => {
    setSavingPw(true);
    try {
      await authApi.changePassword({ currentPassword: curPw, newPassword: newPw });
      setCurPw(""); setNewPw("");
      toast("Password updated.", "success");
    } catch { toast("Failed to update password.", "error"); }
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

  return (
    <div>
      {/* Identity header — avatar + name + inline avatar controls, all on one line */}
      <div className="flex items-center gap-4 rounded-lg border border-border bg-surface/50 p-4">
        <Avatar src={user.avatarUrl} name={user.displayName ?? user.username} size="lg" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display text-xl font-bold text-fg">{user.displayName ?? user.username}</h1>
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

      {/* Dense 2-column grid — everything visible without scrolling on a normal screen */}
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <Panel title="Profile">
          <div className="space-y-3">
            <textarea
              value={bio}
              onChange={(e) => setBio(e.target.value)}
              rows={3}
              placeholder="Bio"
              className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
            />
            <Toggle checked={notifyReply} onChange={setNotifyReply} label="Notify on replies" />
            <Toggle checked={notifyMention} onChange={setNotifyMention} label="Notify on mentions" />
            <Button size="sm" onClick={saveProfile} loading={savingProfile}>Save Profile</Button>
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
            <Button size="sm" onClick={savePassword} loading={savingPw} disabled={!curPw || !newPw}>Change Password</Button>
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

      {cropFile && (
        <ImageCropper file={cropFile} aspect={1} round onCancel={() => setCropFile(null)} onCropped={onCropped} />
      )}
    </div>
  );
}

