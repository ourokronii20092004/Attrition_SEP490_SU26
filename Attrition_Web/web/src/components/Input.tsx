import React from 'react';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement | HTMLTextAreaElement> {
  label?: string;
  error?: string;
  multiline?: boolean;
}

export default function Input({ label, error, multiline, className = '', ...props }: InputProps) {
  return (
    <div className={`input-group ${className}`}>
      {label && <label>{label}</label>}
      {multiline ? (
        <textarea
          className={`input ${error ? 'input-error' : ''}`}
          {...(props as React.TextareaHTMLAttributes<HTMLTextAreaElement>)}
        />
      ) : (
        <input
          className={`input ${error ? 'input-error' : ''}`}
          {...(props as React.InputHTMLAttributes<HTMLInputElement>)}
        />
      )}
      {error && <span style={{ color: 'var(--danger)', fontSize: '12px' }}>{error}</span>}
    </div>
  );
}