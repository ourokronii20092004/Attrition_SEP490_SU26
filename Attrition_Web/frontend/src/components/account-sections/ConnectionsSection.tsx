"use client";

import { useEffect, useState } from "react";
import { Link2, Unlink } from "lucide-react";
import { authApi } from "@/lib/api/auth";
import { parseApiError } from "@/lib/api/parse-error";
import { useAuth, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { API_BASE, GOOGLE_CLIENT_ID } from "@/lib/config";
import { SettingsCard } from "./SettingsCard";

/**
 * Link/unlink a Google account. Connecting uses the same popup-free server-side redirect flow as
 * login (GET /api/auth/google/start?mode=link) — the callback attaches Google to the signed-in
 * user (read from the auth cookie) and bounces back to /settings?linked=1 or ?link_error=…
 * Disconnecting is a plain authenticated POST.
 */
export function ConnectionsSection() {
  const { user, refreshUser } = useAuth();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState("");

  // Handle the redirect back from the OAuth callback, then strip the query so a refresh won't repeat it.
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    // Strip to the current path (not a hardcoded route) so this works from both /settings and the
    // admin account page, which reuses this section.
    if (params.get("linked")) {
      toast("Google connected.", "success");
      refreshUser();
      window.history.replaceState(null, "", window.location.pathname);
    } else if (params.get("link_error")) {
      const m = params.get("link_error")!;
      setMsg(m);
      toast(m, "error");
      window.history.replaceState(null, "", window.location.pathname);
    }
  }, [refreshUser, toast]);

  if (!user) return null;
  const linked = user.isGoogleLinked;

  const link = () => {
    if (!GOOGLE_CLIENT_ID) return;
    // Full-page navigation into the server-side OAuth flow — no popup to be blocked.
    window.location.href = `${API_BASE}/api/auth/google/start?mode=link`;
  };

  const unlink = async () => {
    setBusy(true);
    setMsg("");
    try {
      const res = await authApi.googleUnlink();
      if (res.success) { toast("Google disconnected.", "success"); await refreshUser(); }
      else { setMsg(res.error || "Couldn't disconnect Google."); toast(res.error || "Couldn't disconnect Google.", "error"); }
    } catch (e) {
      const m = parseApiError(e, "Couldn't disconnect Google.");
      setMsg(m);
      toast(m, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <SettingsCard title="Connections">
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <p className="font-medium text-fg">Google</p>
          <p className="text-sm text-fg-muted">
            {linked
              ? user.googleEmail
                // Name the account: it can legitimately differ from the account's own email, and
                // "Connected" alone leaves you guessing which Google account you attached.
                ? `Connected as ${user.googleEmail} — you can sign in with Google.`
                : "Connected — you can sign in with Google."
              : "Not connected."}
          </p>
          {linked && user.googleEmail && user.email && user.googleEmail !== user.email && (
            <p className="mt-1 text-xs text-fg-subtle">
              This differs from your account email ({user.email}). Sign-in works either way; account
              notices go to your account email.
            </p>
          )}
        </div>
        {linked ? (
          <Button variant="secondary" onClick={unlink} loading={busy}>
            <Unlink size={15} className="mr-1.5" /> Disconnect
          </Button>
        ) : (
          <Button variant="secondary" onClick={link} disabled={!GOOGLE_CLIENT_ID}>
            <Link2 size={15} className="mr-1.5" /> Connect
          </Button>
        )}
      </div>
      {linked && !user.hasPassword && (
        <p className="mt-3 text-xs text-warning">
          Set a password above before disconnecting Google, otherwise you won&apos;t be able to sign in.
        </p>
      )}
      {msg && <p className="mt-3 text-sm text-danger">{msg}</p>}
    </SettingsCard>
  );
}
