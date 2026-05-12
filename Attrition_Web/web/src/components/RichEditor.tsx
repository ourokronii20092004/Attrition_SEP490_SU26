'use client';
import dynamic from 'next/dynamic';
import { useTheme } from '@/contexts/ThemeContext';
import rehypeSanitize from 'rehype-sanitize';

const MDEditor = dynamic(
  () => import('@uiw/react-md-editor'),
  { ssr: false }
);

interface RichEditorProps {
  value: string;
  onChange: (value: string) => void;
  height?: number;
}

export default function RichEditor({ value, onChange, height = 400 }: RichEditorProps) {
  const { theme } = useTheme();

  return (
    <div data-color-mode={theme}>
      <MDEditor
        value={value}
        onChange={(val) => onChange(val || '')}
        height={height}
        previewOptions={{
          rehypePlugins: [[rehypeSanitize]],
        }}
      />
    </div>
  );
}