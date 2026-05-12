import React from 'react';

interface AvatarProps {
  src?: string | null;
  username: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}

export default function Avatar({ src, username, size = 'md' }: AvatarProps) {
  const getInitials = (name: string) => name.charAt(0).toUpperCase();

  const getColor = (name: string) => {
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const color = Math.floor(Math.abs((Math.sin(hash) * 16777215) % 16777215)).toString(16);
    return '#' + '000000'.substring(0, 6 - color.length) + color;
  };

  const sizeClass = size === 'md' ? '' : `avatar-${size}`;

  if (src) {
    return <img src={process.env.NEXT_PUBLIC_API_URL ? `${process.env.NEXT_PUBLIC_API_URL}${src}` : src} alt={username} className={`avatar ${sizeClass}`} />;
  }

  return (
    <div
      className={`avatar ${sizeClass}`}
      style={{
        backgroundColor: getColor(username),
        color: '#fff',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: size === 'sm' ? '12px' : size === 'lg' ? '24px' : size === 'xl' ? '36px' : '16px',
        fontWeight: 'bold',
      }}
    >
      {getInitials(username)}
    </div>
  );
}