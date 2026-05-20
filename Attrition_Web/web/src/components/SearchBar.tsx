'use client';
import { useState, useCallback } from 'react';

interface SearchBarProps {
  placeholder?: string;
  onSearch: (query: string) => void;
  debounceMs?: number;
  className?: string;
}

export default function SearchBar({
  placeholder = 'Search...',
  onSearch,
  debounceMs = 300,
  className = '',
}: SearchBarProps) {
  const [value, setValue] = useState('');
  const [timer, setTimer] = useState<ReturnType<typeof setTimeout> | null>(null);

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const newValue = e.target.value;
      setValue(newValue);

      if (timer) clearTimeout(timer);
      const newTimer = setTimeout(() => {
        onSearch(newValue);
      }, debounceMs);
      setTimer(newTimer);
    },
    [onSearch, debounceMs, timer]
  );

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (timer) clearTimeout(timer);
    onSearch(value);
  };

  return (
    <form className={`search-bar ${className}`} onSubmit={handleSubmit}>
      <span className="search-bar-icon">🔍</span>
      <input
        type="text"
        value={value}
        onChange={handleChange}
        placeholder={placeholder}
      />
    </form>
  );
}