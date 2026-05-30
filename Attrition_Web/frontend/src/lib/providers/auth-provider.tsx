"use client";

import { createContext, useContext, useCallback, useEffect, useState, type ReactNode } from "react";
import { authApi } from "@/lib/api/auth";
import { loadTokens, setTokens, clearTokens } from "@/lib/api/client";
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
