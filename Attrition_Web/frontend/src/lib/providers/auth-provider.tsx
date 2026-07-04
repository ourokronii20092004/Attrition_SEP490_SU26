"use client";

import { createContext, useContext, useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { authApi } from "@/lib/api/auth";
import { charactersApi } from "@/lib/api/characters";
import { ApiError } from "@/lib/api/client";
import type { UserDto, LoginRequest, RegisterRequest, AuthResponse } from "@/lib/types";

interface AuthState {
  user: UserDto | null;
  loading: boolean;
}

interface AuthContextValue extends AuthState {
  /** True only during a login/logout transition — drives the full-screen auth loader. */
  transitioning: boolean;
  login: (data: LoginRequest) => Promise<UserDto | null>;
  register: (data: RegisterRequest) => Promise<UserDto | null>;
  loginWithGoogle: (idToken: string) => Promise<AuthResponse | null>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  setUser: (user: UserDto) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ user: null, loading: true });

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

  useEffect(() => {
    // Auth lives in HttpOnly cookies now — we can't read them, so ask the server who we are.
    // A 401 (no/expired cookie) simply resolves to logged-out.
    authApi
      .me()
      .then((res) => {
        setState({ user: res.success && res.data ? res.data : null, loading: false });
      })
      .catch(() => {
        setState({ user: null, loading: false });
      });
  }, []);

  // Drop the user to a clean logged-out state when a token refresh fails mid-session.
  useEffect(() => {
    const onExpired = () => setState({ user: null, loading: false });
    window.addEventListener("attrition:session-expired", onExpired);
    return () => window.removeEventListener("attrition:session-expired", onExpired);
  }, []);

  // Enforce bans mid-session: poll the session-check endpoint; a banned account (403) is logged out.
  useEffect(() => {
    if (!state.user) return;
    let cancelled = false;
    const check = async () => {
      try {
        const res = await charactersApi.sessionCheck();
        if (!cancelled && res.success && res.data?.isBanned) {
          setState({ user: null, loading: false });
        }
      } catch (err) {
        // Only force logout on an auth failure (banned/unauthorized); ignore transient errors.
        const status = err instanceof ApiError ? err.status : 0;
        if (!cancelled && (status === 401 || status === 403)) {
          setState({ user: null, loading: false });
        }
      }
    };
    const interval = setInterval(check, 60_000);
    return () => { cancelled = true; clearInterval(interval); };
  }, [state.user]);

  const login = useCallback(async (data: LoginRequest) => {
    beginTransition();
    try {
      const res = await authApi.login(data);
      if (res.success && res.data) {
        setState({ user: res.data.user, loading: false });
        endTransition(); // keep the loader up briefly to cover the post-login redirect
        return res.data.user;
      }
      endTransition(0);
      return null;
    } catch (e) {
      endTransition(0); // drop the loader immediately so the form can show the error
      throw e;
    }
  }, [beginTransition, endTransition]);

  const register = useCallback(async (data: RegisterRequest) => {
    beginTransition();
    try {
      const res = await authApi.register(data);
      if (res.success && res.data) {
        setState({ user: res.data.user, loading: false });
        endTransition();
        return res.data.user;
      }
      endTransition(0);
      return null;
    } catch (e) {
      endTransition(0);
      throw e;
    }
  }, [beginTransition, endTransition]);

  const loginWithGoogle = useCallback(async (idToken: string) => {
    beginTransition();
    try {
      const res = await authApi.google({ code: idToken, redirectUri: window.location.origin });
      if (res.success && res.data) {
        setState({ user: res.data.user, loading: false });
        endTransition();
        return res.data;
      }
      endTransition(0);
      return null;
    } catch (e) {
      endTransition(0);
      throw e;
    }
  }, [beginTransition, endTransition]);

  const logout = useCallback(() => {
    beginTransition();
    authApi.logout().catch(() => {});
    setState({ user: null, loading: false });
    endTransition();
  }, [beginTransition, endTransition]);

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
