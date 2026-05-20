'use client';
import { ReactNode } from 'react';

interface BadgeProps {
  children: ReactNode;
  variant?: 'admin' | 'mod' | 'user' | 'pinned' | 'locked' | 'ember' | 'success' | 'danger' | 'info' | 'warning';
  className?: string;
}

export default function Badge({ children, variant = 'user', className = '' }: BadgeProps) {
  return (
    <span className={`badge badge-${variant} ${className}`}>
      {children}
    </span>
  );
}