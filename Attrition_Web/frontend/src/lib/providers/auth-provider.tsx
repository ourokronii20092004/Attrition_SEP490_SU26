"use client";

import { createContext, useContext, useCallback, useEffect, useState, type ReactNode } from "react";
import { authApi } from "@/lib/api/auth";
import { charactersApi } from "@/lib/api/characters";
import { loadTokens, setTokens, clearTokens, getAccessToken, ApiError } from "@/lib/api/client";
import type { UserDto, LoginRequest, RegisterRequest } from "@/lib/types";

interface AuthState {
  user: UserDto | null;
  loading: boolean;
}

interface AuthContextValue extends AuthState {
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  loginWithGoogle: (idToken: string) => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  setUser: (user: UserDto) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ user: null, loading: true });

  useEffect(() => {
    loadTokens();
    // Skip the guaranteed-401 /me call when there's no token (logged out).
    if (!getAccessToken()) {
      setState({ user: null, loading: false });
      return;
    }
    authApi
      .me()
      .then((res) => {
        if (res.success && res.data) {
          setState({ user: res.data, loading: false });
        } else {
          setState({ user: null, loading: false });
        }
      })
      .catch(() => {
        setState({ user: null, loading: false });
      });
  }, []);

  // Drop the user to a clean logged-out state when a token refresh fails mid-session.
  useEffect(() => {
    const onExpired = () => {
      clearTokens();
      setState({ user: null, loading: false });
    };
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
          clearTokens();
          setState({ user: null, loading: false });
        }
      } catch (err) {
        // Only force logout on an auth failure (banned/unauthorized); ignore transient errors.
        const status = err instanceof ApiError ? err.status : 0;
        if (!cancelled && (status === 401 || status === 403)) {
          clearTokens();
          setState({ user: null, loading: false });
        }
      }
    };
    const interval = setInterval(check, 60_000);
    return () => { cancelled = true; clearInterval(interval); };
  }, [state.user]);

  const login = useCallback(async (data: LoginRequest) => {
    const res = await authApi.login(data);
    if (res.success && res.data) {
      setTokens(res.data.accessToken, res.data.refreshToken);
      setState({ user: res.data.user, loading: false });
    }
  }, []);

  const register = useCallback(async (data: RegisterRequest) => {
    const res = await authApi.register(data);
    if (res.success && res.data) {
      setTokens(res.data.accessToken, res.data.refreshToken);
      setState({ user: res.data.user, loading: false });
    }
  }, []);

  const loginWithGoogle = useCallback(async (idToken: string) => {
    const res = await authApi.google({ code: idToken, redirectUri: window.location.origin });
    if (res.success && res.data) {
      setTokens(res.data.accessToken, res.data.refreshToken);
      setState({ user: res.data.user, loading: false });
    }
  }, []);

  const logout = useCallback(() => {
    authApi.logout().catch(() => {});
    clearTokens();
    setState({ user: null, loading: false });
  }, []);

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
    <AuthContext.Provider value={{ ...state, login, register, loginWithGoogle, logout, refreshUser, setUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
