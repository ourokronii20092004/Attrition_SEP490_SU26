"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { authApi } from "@/lib/api/auth";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";

const ACCENTS = ["crimson", "ember", "gold", "emerald", "azure", "violet", "rose"];

export default function SettingsPage() {
  const { user, loading, refreshUser, logout, setUser } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  if (loading || !user) return <PageLoader />;

  return (
    <div className="mx-auto max-w-2xl px-4 py-8">
      <h1 className="font-display text-3xl font-bold text-fg">Settings</h1>

      <div className="mt-8 space-y-10">
        <ProfileSection user={user} refreshUser={refreshUser} setUser={setUser} />
        <AvatarSection user={user} refreshUser={refreshUser} />
        <ThemeSection user={user} refreshUser={refreshUser} />
        <PasswordSection />
        <EmailSection user={user} refreshUser={refreshUser} />
        <DangerSection logout={logout} />
      </div>
    </div>
  );
}

function ProfileSection({ user, refreshUser, setUser }: any) {
  const [bio, setBio] = useState(user.bio ?? "");
  const [notifyOnReply, setNotifyOnReply] = useState(user.notifyOnReply ?? true);
  const [notifyOnMention, setNotifyOnMention] = useState(user.notifyOnMention ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await accountApi.updateProfile({ bio, notifyOnReply, notifyOnMention });
      if (res.success && res.data) setUser(res.data);
    } catch {
      setError("Failed to save profile. Please try again.");
    }
    setSaving(false);
  };

  return (
    <section>
      <h2 className="text-lg font-semibold text-fg">Profile</h2>
      <div className="mt-4 space-y-3">
        <div className="space-y-1">
          <label className="block text-sm font-medium text-fg-muted">Bio</label>
          <textarea
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            rows={3}
            className="w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent"
          />
        </div>
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm text-fg-muted">
            <input
              type="checkbox"
              checked={notifyOnReply}
              onChange={(e) => setNotifyOnReply(e.target.checked)}
              className="rounded border-border"
            />
            Notify on replies
          </label>
          <label className="flex items-center gap-2 text-sm text-fg-muted">
            <input
              type="checkbox"
              checked={notifyOnMention}
              onChange={(e) => setNotifyOnMention(e.target.checked)}
              className="rounded border-border"
            />
            Notify on mentions
          </label>
        </div>
        <Button onClick={save} loading={saving}>Save Profile</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </section>
  );
}

function AvatarSection({ user, refreshUser }: any) {
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setError("");
    try {
      await accountApi.uploadAvatar(file);
      await refreshUser();
    } catch {
      setError("Failed to upload avatar. Please try again.");
    }
    setUploading(false);
  };

  const handleDelete = async () => {
    setError("");
    try {
      await accountApi.deleteAvatar();
      await refreshUser();
    } catch {
      setError("Failed to remove avatar. Please try again.");
    }
  };

  return (
    <section>
      <h2 className="text-lg font-semibold text-fg">Avatar & Background</h2>
      <div className="mt-4 flex items-center gap-4">
        {user.avatarUrl ? (
          <img src={resolveMediaUrl(user.avatarUrl) ?? ""} alt="" className="h-16 w-16 rounded-full object-cover" />
        ) : (
          <div className="flex h-16 w-16 items-center justify-center rounded-full bg-surface-3 text-fg-muted">?</div>
        )}
        <div className="space-y-2">
          <label className="cursor-pointer rounded-md border border-border px-3 py-1.5 text-sm text-fg-muted hover:bg-surface-2">
            {uploading ? "Uploading..." : "Upload Avatar"}
            <input type="file" accept="image/*" onChange={handleUpload} className="hidden" />
          </label>
          {user.avatarUrl && (
            <button onClick={handleDelete} className="ml-2 text-sm text-danger hover:underline">Remove</button>
          )}
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
    </section>
  );
}

function ThemeSection({ user, refreshUser }: any) {
  const [mode, setMode] = useState(user.themeMode ?? "dark");
  const [accent, setAccent] = useState(user.themeAccent ?? "crimson");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      await accountApi.updateTheme({ themeMode: mode, themeAccent: accent });
      await refreshUser();
    } catch {
      setError("Failed to save theme. Please try again.");
    }
    setSaving(false);
  };

  return (
    <section>
      <h2 className="text-lg font-semibold text-fg">Theme</h2>
      <div className="mt-4 space-y-4">
        <div className="flex gap-3">
          <button
            onClick={() => setMode("dark")}
            className={`rounded-md px-4 py-2 text-sm ${mode === "dark" ? "bg-accent text-accent-fg" : "border border-border text-fg-muted"}`}
          >
            Dark
          </button>
          <button
            onClick={() => setMode("light")}
            className={`rounded-md px-4 py-2 text-sm ${mode === "light" ? "bg-accent text-accent-fg" : "border border-border text-fg-muted"}`}
          >
            Light
          </button>
        </div>
        <div className="flex flex-wrap gap-2">
          {ACCENTS.map((a) => (
            <button
              key={a}
              onClick={() => setAccent(a)}
              className={`rounded-md px-3 py-1.5 text-sm capitalize ${accent === a ? "bg-accent text-accent-fg" : "border border-border text-fg-muted"}`}
            >
              {a}
            </button>
          ))}
        </div>
        <Button onClick={save} loading={saving}>Save Theme</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </section>
  );
}

function PasswordSection() {
  const [current, setCurrent] = useState("");
  const [newPw, setNewPw] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await authApi.changePassword({ currentPassword: current, newPassword: newPw });
      setMsg("Password updated");
      setCurrent("");
      setNewPw("");
    } catch {
      setMsg("Failed to update password");
    }
    setSaving(false);
  };

  return (
    <section>
      <h2 className="text-lg font-semibold text-fg">Password</h2>
      <div className="mt-4 space-y-3">
        <Input label="Current Password" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        <Input label="New Password" type="password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!current || !newPw}>Change Password</Button>
      </div>
    </section>
  );
}

function EmailSection({ user, refreshUser }: any) {
  const [email, setEmail] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await accountApi.updateEmail({ newEmail: email, currentPassword });
      setMsg("Verification email sent to new address");
      await refreshUser();
    } catch {
      setMsg("Failed to update email");
    }
    setSaving(false);
  };

  return (
    <section>
      <h2 className="text-lg font-semibold text-fg">Email</h2>
      <p className="mt-1 text-sm text-fg-muted">Current: {user.email}</p>
      <div className="mt-4 space-y-3">
        <Input label="New Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <Input label="Current Password" type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!email || !currentPassword}>Update Email</Button>
      </div>
    </section>
  );
}

function DangerSection({ logout }: { logout: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");

  const handleDelete = async () => {
    setDeleting(true);
    setError("");
    try {
      await accountApi.deleteAccount();
      logout();
    } catch {
      setError("Failed to delete account. Please try again.");
      setDeleting(false);
    }
  };

  return (
    <section className="border-t border-border pt-6">
      <h2 className="text-lg font-semibold text-danger">Danger Zone</h2>
      {!confirming ? (
        <Button variant="danger" className="mt-4" onClick={() => setConfirming(true)}>Delete Account</Button>
      ) : (
        <div className="mt-4 space-y-2">
          <p className="text-sm text-fg-muted">This action is irreversible. Are you sure?</p>
          <div className="flex gap-2">
            <Button variant="danger" onClick={handleDelete} loading={deleting}>Yes, Delete</Button>
            <Button variant="secondary" onClick={() => setConfirming(false)}>Cancel</Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      )}
    </section>
  );
}
