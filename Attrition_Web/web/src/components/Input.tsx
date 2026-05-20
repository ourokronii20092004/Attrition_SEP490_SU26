'use client';
import { InputHTMLAttributes, TextareaHTMLAttributes } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  textarea?: boolean;
}

export default function Input({ label, error, textarea, className = '', ...props }: InputProps) {
  return (
    <div className="input-group">
      {label && <label>{label}</label>}
      {textarea ? (
        <textarea
          className={`input ${error ? 'input-error' : ''} ${className}`}
          {...(props as TextareaHTMLAttributes<HTMLTextAreaElement>)}
        />
      ) : (
        <input
          className={`input ${error ? 'input-error' : ''} ${className}`}
          {...props}
        />
      )}
      {error && <span className="text-blood" style={{ fontSize: '13px' }}>{error}</span>}
    </div>
  );
}