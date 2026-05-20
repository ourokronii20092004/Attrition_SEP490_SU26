'use client';
import dynamic from 'next/dynamic';
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
  return (
    <div data-color-mode="dark">
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