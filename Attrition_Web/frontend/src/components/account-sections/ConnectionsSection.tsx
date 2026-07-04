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
    if (params.get("linked")) {
      toast("Google connected.", "success");
      refreshUser();
      window.history.replaceState(null, "", "/settings");
    } else if (params.get("link_error")) {
      const m = params.get("link_error")!;
      setMsg(m);
      toast(m, "error");
      window.history.replaceState(null, "", "/settings");
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
            {linked ? "Connected — you can sign in with Google." : "Not connected."}
          </p>
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
