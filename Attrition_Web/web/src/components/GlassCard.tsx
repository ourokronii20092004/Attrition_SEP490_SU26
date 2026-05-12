import React from 'react';

export default function GlassCard({ children, className = '', style = {} }: { children: React.ReactNode, className?: string, style?: React.CSSProperties }) {
  return (
    <div className={`glass-card ${className}`} style={style}>
      {children}
    </div>
  );
}