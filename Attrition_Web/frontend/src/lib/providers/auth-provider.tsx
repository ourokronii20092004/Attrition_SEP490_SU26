"use client";

import { createContext, useContext, useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { authApi } from "@/lib/api/auth";
import { charactersApi } from "@/lib/api/characters";
import { ApiError } from "@/lib/api/client";
import { takePendingRedirect } from "@/lib/post-login-redirect";
import { useToast } from "./toast-provider";
import type { UserDto, LoginRequest, RegisterRequest, AuthResponse } from "@/lib/types";

interface AuthState {
  user: UserDto | null;
  loading: boolean;
}

interface AuthContextValue extends AuthState {
  /** True only during a login/logout transition — drives the full-screen auth loader. */
  transitioning: boolean;
  login: (data: LoginRequest) => Promise<UserDto | null>;
  /** Registers the account. Does NOT sign the user in — the email must be verified first. */
  register: (data: RegisterRequest) => Promise<UserDto | null>;
  loginWithGoogle: (idToken: string) => Promise<AuthResponse | null>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  setUser: (user: UserDto) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

// Cross-tab auth sync: logging in/out in one tab broadcasts here so other tabs (e.g. an open forum
// thread) update immediately instead of leaving a stale, half-authed UI until their next request.
const AUTH_BROADCAST_KEY = "attrition:auth-broadcast";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ user: null, loading: true });
  const { toast } = useToast();
  const router = useRouter();

  // Mirror the current user in a ref so window/storage listeners (registered once) can read the
  // latest value without being torn down and re-added on every user change.
  const userRef = useRef<UserDto | null>(null);
  useEffect(() => { userRef.current = state.user; }, [state.user]);

  // The full-screen loader is shown ONLY during an explicit login/logout, never on ordinary page
  // loads. It's kept up briefly after the action to cover the ensuing navigation/re-render.
  const [transitioning, setTransitioning] = useState(false);
  const transitionTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const beginTransition = useCallback(() => {
    if (transitionTimer.current) clearTimeout(transitionTimer.current);
    setTransitioning(true);
  }, []);
  const endTransition = useCallback((delayMs = 900) => {
    if (transitionTimer.current) clearTimeout(transitionTimer.current);
    if (delayMs <= 0) { setTransitioning(false); return; }
    transitionTimer.current = setTimeout(() => setTransitioning(false), delayMs);
  }, []);

  const broadcast = useCallback((type: "login" | "logout") => {
    try { localStorage.setItem(AUTH_BROADCAST_KEY, JSON.stringify({ type, t: Date.now() })); } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    // Auth lives in HttpOnly cookies now — we can't read them, so ask the server who we are.
    // A 401 (no/expired cookie) simply resolves to logged-out.
    authApi
      .me()
      .then((res) => {
        const user = res.success && res.data ? res.data : null;
        setState({ user, loading: false });
        // Google's callback drops the browser on "/" with no way to carry ?redirect through the
        // OAuth round-trip, so the destination was stashed before leaving. Consume it here —
        // this is the moment we learn the round-trip succeeded. Only fires when something was
        // actually stashed, so a normal visit to "/" is unaffected.
        if (user) {
          const back = takePendingRedirect();
          if (back) router.replace(back);
        }
      })
      .catch(() => {
        setState({ user: null, loading: false });
      });
  }, [router]);

  // Drop the user to a clean logged-out state when a token refresh fails mid-session, and tell them
  // why (their session expired) — but only if they were actually signed in, so first-load visitors
  // (whose initial /me 401s) never see a spurious notice.
  useEffect(() => {
    const onExpired = () => {
      if (userRef.current) toast("Your session has expired. Please sign in again.", "info");
      setState({ user: null, loading: false });
    };
    window.addEventListener("attrition:session-expired", onExpired);
    return () => window.removeEventListener("attrition:session-expired", onExpired);
  }, [toast]);

  // Cross-tab sync: react to login/logout broadcast from a sibling tab.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key !== AUTH_BROADCAST_KEY || !e.newValue) return;
      let msg: { type?: string } = {};
      try { msg = JSON.parse(e.newValue); } catch { return; }
      if (msg.type === "logout") {
        if (userRef.current) { toast("You've been signed out.", "info"); }
        setState({ user: null, loading: false });
      } else if (msg.type === "login") {
        authApi.me().then((res) => {
          if (res.success && res.data) setState({ user: res.data, loading: false });
        }).catch(() => { /* ignore */ });
      }
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, [toast]);

  // Enforce bans + password-change revocation mid-session: poll the session-check endpoint. A banned
  // account (403) or a revoked session (401, token minted before a password change) is signed out.
  useEffect(() => {
    if (!state.user) return;
    let cancelled = false;
    const check = async () => {
      try {
        const res = await charactersApi.sessionCheck();
        if (!cancelled && res.success && res.data?.isBanned) {
          toast("Your account has been suspended.", "error");
          setState({ user: null, loading: false });
        }
      } catch (err) {
        // Force logout on an auth failure (banned/unauthorized/revoked); ignore transient errors.
        const status = err instanceof ApiError ? err.status : 0;
        if (!cancelled && (status === 401 || status === 403)) {
          if (userRef.current) toast("Your session has ended. Please sign in again.", "info");
          setState({ user: null, loading: false });
        }
      }
    };
    const interval = setInterval(check, 60_000);
    return () => { cancelled = true; clearInterval(interval); };
  }, [state.user, toast]);

  const login = useCallback(async (data: LoginRequest) => {
    beginTransition();
    try {
      const res = await authApi.login(data);
      if (res.success && res.data) {
        setState({ user: res.data.user, loading: false });
        broadcast("login");
        endTransition(); // keep the loader up briefly to cover the post-login redirect
        return res.data.user;
      }
      endTransition(0);
      return null;
    } catch (e) {
      endTransition(0); // drop the loader immediately so the form can show the error
      throw e;
    }
  }, [beginTransition, endTransition, broadcast]);

  // Registration no longer signs the user in: the account must verify its email first (the server
  // won't issue a session for an unverified local account). We return the created user only so the
  // page can confirm success and route to the "verify your email" screen.
  const register = useCallback(async (data: RegisterRequest) => {
    const res = await authApi.register(data);
    return res.success && res.data ? res.data.user : null;
  }, []);

  const loginWithGoogle = useCallback(async (idToken: string) => {
    beginTransition();
    try {
      const res = await authApi.google({ code: idToken, redirectUri: window.location.origin });
      if (res.success && res.data) {
        setState({ user: res.data.user, loading: false });
        broadcast("login");
        endTransition();
        return res.data;
      }
      endTransition(0);
      return null;
    } catch (e) {
      endTransition(0);
      throw e;
    }
  }, [beginTransition, endTransition, broadcast]);

  const logout = useCallback(() => {
    beginTransition();
    authApi.logout().catch(() => {});
    setState({ user: null, loading: false });
    broadcast("logout");
    endTransition();
  }, [beginTransition, endTransition, broadcast]);

  const refreshUser = useCallback(async () => {
    const res = await authApi.me();
    if (res.success && res.data) {
      setState((s) => ({ ...s, user: res.data }));
    }
  }, []);

  const setUser = useCallback((user: UserDto) => {
    setState((s) => ({ ...s, user }));
  }, []);

  return (
    <AuthContext.Provider value={{ ...state, transitioning, login, register, loginWithGoogle, logout, refreshUser, setUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
