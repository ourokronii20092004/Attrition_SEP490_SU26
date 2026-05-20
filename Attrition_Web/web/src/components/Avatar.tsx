'use client';

interface AvatarProps {
  src?: string | null;
  alt?: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
}

export default function Avatar({ src, alt = '', size = 'md', className = '' }: AvatarProps) {
  const sizeClass = size !== 'md' ? `avatar-${size}` : '';
  const initial = alt ? alt.charAt(0).toUpperCase() : '?';

  if (src) {
    return (
      <img
        src={src}
        alt={alt}
        className={`avatar ${sizeClass} ${className}`}
      />
    );
  }

  return (
    <div className={`avatar avatar-placeholder ${sizeClass} ${className}`}>
      {initial}
    </div>
  );
}