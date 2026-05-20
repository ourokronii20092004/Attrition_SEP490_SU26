'use client';

import { AuthProvider as AuthProviderBase } from '@/contexts/AuthContext';
import { ReactNode } from 'react';

export function AuthProvider({ children }: { children: ReactNode }) {
  return <AuthProviderBase>{children}</AuthProviderBase>;
}
