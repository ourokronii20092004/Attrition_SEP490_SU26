import { clsx } from "clsx";
import { resolveMediaUrl } from "@/lib/api/media";

const SIZES = {
  sm: "h-8 w-8 text-xs",
  md: "h-10 w-10 text-sm",
  lg: "h-16 w-16 text-lg",
  xl: "h-24 w-24 text-2xl",
} as const;

interface AvatarProps {
  src?: string | null;
  name?: string | null;
  size?: keyof typeof SIZES;
  className?: string;
}

/** User avatar: resolved image, or a tinted corruption-glyph initial fallback. */
export function Avatar({ src, name, size = "md", className }: AvatarProps) {
  const url = resolveMediaUrl(src ?? undefined);
  const initial = (name ?? "?").trim().charAt(0).toUpperCase() || "?";
  return (
    <span
      className={clsx(
        "inline-flex shrink-0 select-none items-center justify-center overflow-hidden rounded-full",
        "border border-accent/20 bg-accent-soft font-display font-semibold text-accent",
        SIZES[size],
        className,
      )}
    >
      {url ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={url} alt={name ?? ""} className="h-full w-full object-cover" />
      ) : (
        initial
      )}
    </span>
  );
}
