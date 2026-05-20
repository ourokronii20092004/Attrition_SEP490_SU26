'use client';
import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { api } from '@/lib/api';

interface User {
  id: string;
  username: string;
  role: string;
  avatarUrl: string | null;
  bio: string | null;
  mustChangePassword: boolean;
}

interface AuthContextType {
  user: User | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<{ success: boolean; error?: string }>;
  register: (username: string, password: string, email?: string) => Promise<{ success: boolean; error?: string }>;
  googleLogin: (code: string, redirectUri: string) => Promise<{ success: boolean; error?: string }>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('attrition-token');
    if (token) {
      refreshUser().finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  async function login(username: string, password: string) {
    const res = await api.post('/api/auth/login', { username, password });
    if (res.success) {
      localStorage.setItem('attrition-token', res.data.accessToken);
      localStorage.setItem('attrition-refresh', res.data.refreshToken);
      setUser(res.data.user);
      return { success: true };
    }
    return { success: false, error: res.error };
  }

  async function register(username: string, password: string, email?: string) {
    const res = await api.post('/api/auth/register', { username, password, email });
    if (res.success) {
      localStorage.setItem('attrition-token', res.data.accessToken);
      localStorage.setItem('attrition-refresh', res.data.refreshToken);
      setUser(res.data.user);
      return { success: true };
    }
    return { success: false, error: res.error };
  }

  async function googleLogin(code: string, redirectUri: string) {
    const res = await api.post('/api/auth/google', { code, redirectUri });
    if (res.success) {
      localStorage.setItem('attrition-token', res.data.accessToken);
      localStorage.setItem('attrition-refresh', res.data.refreshToken);
      setUser(res.data.user);
      return { success: true };
    }
    return { success: false, error: res.error };
  }

  function logout() {
    localStorage.removeItem('attrition-token');
    localStorage.removeItem('attrition-refresh');
    setUser(null);
  }

  async function refreshUser() {
    const res = await api.get('/api/auth/me');
    if (res.success) setUser(res.data);
    else logout();
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, googleLogin, logout, refreshUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
};