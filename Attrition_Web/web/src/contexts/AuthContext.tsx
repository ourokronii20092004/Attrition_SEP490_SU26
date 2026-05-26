"use client";

import React, { createContext, useContext, useEffect, useState, useCallback, useRef } from "react";
import { api, setTokens, clearTokens, getAccessToken } from "@/lib/api";
import { useTheme } from "./ThemeContext";

// ─── Types ─────────────────────────────────────────────────

export interface User {
  id: string;
  username: string;
  email: string | null;
  displayName: string | null;
  role: "User" | "Admin";
  avatarUrl: string | null;
  backgroundUrl: string | null;
  themeMode: "light" | "dark" | "system";
  themeAccent: string;
  bio: string | null;
  authProvider: "local" | "google" | "linked";
  joinedAt: string;
  postCount: number;
  contributionCount: number;
  mustChangePassword: boolean;
  isEmailVerified: boolean;
  pendingEmail: string | null;
  notifyOnReply: boolean;
  notifyOnMention: boolean;
}

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: User;
}

interface AuthContextValue {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, password: string, email?: string) => Promise<void>;
  googleLogin: (idToken: string, redirectUri: string) => Promise<void>;
  linkGoogle: (idToken: string) => Promise<void>;
  unlinkGoogle: () => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

// ─── Provider ──────────────────────────────────────────────

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { mode, accent, setMode, setAccent, resetTheme } = useTheme();

  // Sync theme from user profile on login/load
  useEffect(() => {
    if (user) {
      if (user.themeMode && user.themeMode !== mode) setMode(user.themeMode as any);
      if (user.themeAccent && user.themeAccent !== accent) setAccent(user.themeAccent as any);
    }
  }, [user?.id]);

  // Sync theme to backend when changed locally
  const isInitialMount = useRef(true);
  useEffect(() => {
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }
    if (user && !isLoading) {
      api.put("/users/theme", { themeMode: mode, themeAccent: accent }).catch(() => {});
    }
  }, [mode, accent]);

  // Initial load: check for existing token
  useEffect(() => {
    const token = getAccessToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    api
      .get<User>("/auth/me")
      .then((res) => {
        if (res.success && res.data) {
          setUser(res.data);
        } else {
          clearTokens();
        }
      })
      .catch(() => {
        clearTokens();
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  // Poll for auth state changes across tabs/domains
  useEffect(() => {
    const interval = setInterval(() => {
      const currentToken = getAccessToken();
      
      // If we have a user but token is gone (logged out elsewhere)
      if (user && !currentToken) {
        setUser(null);
        resetTheme();
      }
      
      // If we don't have a user but token appeared (logged in elsewhere)
      // We should probably fetch the user
      if (!user && currentToken && !isLoading) {
        api.get<User>("/auth/me").then((res) => {
          if (res.success && res.data) setUser(res.data);
        }).catch(() => {});
      }
    }, 2000);
    return () => clearInterval(interval);
  }, [user, isLoading]);

  const handleAuthResponse = useCallback((data: AuthResponse) => {
    setTokens(data.accessToken, data.refreshToken);
    setUser(data.user);
  }, []);

  const login = useCallback(
    async (username: string, password: string) => {
      const res = await api.post<AuthResponse>("/auth/login", {
        username,
        password,
      });
      if (res.success && res.data) {
        handleAuthResponse(res.data);
      } else {
        throw new Error(res.error || "Login failed");
      }
    },
    [handleAuthResponse]
  );

  const register = useCallback(
    async (username: string, password: string, email?: string) => {
      const res = await api.post<AuthResponse>("/auth/register", {
        username,
        password,
        email: email || undefined,
      });
      if (res.success && res.data) {
        handleAuthResponse(res.data);
      } else {
        throw new Error(res.error || "Registration failed");
      }
    },
    [handleAuthResponse]
  );

  const googleLogin = useCallback(
    async (idToken: string, redirectUri: string) => {
      const res = await api.post<AuthResponse>("/auth/google", {
        code: idToken,
        redirectUri,
      });
      if (res.success && res.data) {
        handleAuthResponse(res.data);
      } else {
        throw new Error(res.error || "Google login failed");
      }
    },
    [handleAuthResponse]
  );

  const linkGoogle = useCallback(async (idToken: string) => {
    const res = await api.post<User>("/auth/google/link", { idToken });
    if (res.success && res.data) {
      setUser(res.data);
    } else {
      throw new Error(res.error || "Failed to link Google account");
    }
  }, []);

  const unlinkGoogle = useCallback(async () => {
    const res = await api.post<User>("/auth/google/unlink");
    if (res.success && res.data) {
      setUser(res.data);
    } else {
      throw new Error(res.error || "Failed to unlink Google account");
    }
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post("/auth/logout");
    } catch {}
    clearTokens();
    setUser(null);
    resetTheme();
    // Don't redirect here — let the caller decide
  }, [resetTheme]);

  const refreshUser = useCallback(async () => {
    try {
      const res = await api.get<User>("/auth/me");
      if (res.success && res.data) {
        setUser(res.data);
      }
    } catch {
      // Silently fail — user might be offline
    }
  }, []);

  const changePassword = useCallback(
    async (currentPassword: string, newPassword: string) => {
      const res = await api.post("/auth/change-password", {
        currentPassword,
        newPassword,
      });
      if (!res.success) {
        throw new Error(res.error || "Failed to change password");
      }
      // Update mustChangePassword flag
      if (user) {
        setUser({ ...user, mustChangePassword: false });
      }
    },
    [user]
  );

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user,
        isAdmin: user?.role === "Admin",
        login,
        register,
        googleLogin,
        linkGoogle,
        unlinkGoogle,
        logout,
        refreshUser,
        changePassword,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// ─── Hook ──────────────────────────────────────────────────

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
