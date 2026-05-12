import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import rehypeSanitize from 'rehype-sanitize';

interface MarkdownRendererProps {
  content: string;
}

export default function MarkdownRenderer({ content }: MarkdownRendererProps) {
  return (
    <div className="markdown-body" style={{ color: 'var(--text-primary)' }}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeRaw, rehypeSanitize]}
        components={{
          code({ node, className, children, ...props }: any) {
            const match = /language-(\w+)/.exec(className || '');
            const isInline = !match && !String(children).includes('\n');
            return isInline ? (
              <code className={className} style={{ background: 'var(--bg-tertiary)', padding: '2px 4px', borderRadius: '4px', fontFamily: 'var(--font-mono)', fontSize: '0.875em' }} {...props}>
                {children}
              </code>
            ) : (
              <div style={{ background: 'var(--bg-tertiary)', padding: 'var(--space-md)', borderRadius: 'var(--glass-radius-sm)', overflowX: 'auto', marginBottom: 'var(--space-md)' }}>
                <code className={className} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.875em' }} {...props}>
                  {children}
                </code>
              </div>
            );
          },
          a: ({ node, ...props }) => <a style={{ color: 'var(--accent)', textDecoration: 'underline' }} {...props} />,
          h1: ({ node, ...props }) => <h1 style={{ marginTop: 'var(--space-lg)', marginBottom: 'var(--space-md)', borderBottom: '1px solid var(--border)', paddingBottom: 'var(--space-sm)' }} {...props} />,
          h2: ({ node, ...props }) => <h2 style={{ marginTop: 'var(--space-lg)', marginBottom: 'var(--space-sm)' }} {...props} />,
          h3: ({ node, ...props }) => <h3 style={{ marginTop: 'var(--space-md)', marginBottom: 'var(--space-sm)' }} {...props} />,
          p: ({ node, ...props }) => <p style={{ marginBottom: 'var(--space-md)' }} {...props} />,
          ul: ({ node, ...props }) => <ul style={{ paddingLeft: 'var(--space-lg)', marginBottom: 'var(--space-md)' }} {...props} />,
          ol: ({ node, ...props }) => <ol style={{ paddingLeft: 'var(--space-lg)', marginBottom: 'var(--space-md)' }} {...props} />,
          li: ({ node, ...props }) => <li style={{ marginBottom: 'var(--space-xs)' }} {...props} />,
          blockquote: ({ node, ...props }) => <blockquote style={{ borderLeft: '4px solid var(--accent)', paddingLeft: 'var(--space-md)', color: 'var(--text-secondary)', fontStyle: 'italic', margin: '0 0 var(--space-md) 0' }} {...props} />,
          table: ({ node, ...props }) => <div style={{ overflowX: 'auto', marginBottom: 'var(--space-md)' }}><table style={{ width: '100%', borderCollapse: 'collapse' }} {...props} /></div>,
          th: ({ node, ...props }) => <th style={{ border: '1px solid var(--border)', padding: '8px', background: 'var(--bg-tertiary)', textAlign: 'left' }} {...props} />,
          td: ({ node, ...props }) => <td style={{ border: '1px solid var(--border)', padding: '8px' }} {...props} />,
          img: ({ node, ...props }) => <img style={{ maxWidth: '100%', height: 'auto', borderRadius: 'var(--glass-radius-sm)' }} {...props} alt={props.alt || ''} />
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}