import React from 'react';

interface BadgeProps {
  variant: 'admin' | 'user' | 'pinned' | 'locked' | 'success' | 'warning' | 'danger';
  children: React.ReactNode;
}

export default function Badge({ variant, children }: BadgeProps) {
  return (
    <span className={`badge badge-${variant}`}>
      {children}
    </span>
  );
}