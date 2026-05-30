import { SITE_NAME } from "@/lib/config";

export function Footer() {
  return (
    <footer className="border-t border-border bg-surface/50 py-8">
      <div className="mx-auto max-w-7xl px-4 text-center text-sm text-fg-muted">
        <p>&copy; {new Date().getFullYear()} {SITE_NAME}. A thesis project — SEP490 SU26.</p>
      </div>
    </footer>
  );
}
