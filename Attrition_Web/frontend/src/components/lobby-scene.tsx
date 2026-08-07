"use client";

import { clsx } from "clsx";
import lobby from "@/content/assets/game-lobby-background.png";

/**
 * The in-game lobby art, recolored at runtime to whatever accent the visitor has chosen.
 *
 * The site lets users pick from eleven accents plus light/dark, so any fixed-color photograph
 * eventually fights someone's theme — a cool blue-grey landscape under a crimson or gold accent
 * reads as a mistake. Rather than drop the art or freeze one theme, the image is reduced to a
 * luminance mask and re-tinted from `--color-accent`, so it is never its own color: it is always
 * the current one.
 *
 * How it works: `grayscale` flattens the source, then an accent-filled layer in `color` blend mode
 * pushes the live accent hue through it while keeping the pixel-art luminance intact. A second
 * `multiply`/`screen` pass seats it against the page surface so it reads as printed onto the page
 * instead of pasted over it. Edges fade to transparent through a mask, so there is no hard seam
 * against whichever background is active.
 *
 * `aria-hidden` throughout: this is atmosphere, and the surrounding copy already carries the
 * meaning a screen reader needs.
 */
export function LobbyScene({
  className,
  intensity = "hero",
  priority = false,
}: {
  className?: string;
  /** `hero` is the full-strength banner; `quiet` is the muted variant for interior page headers. */
  intensity?: "hero" | "quiet";
  priority?: boolean;
}) {
  const hero = intensity === "hero";

  return (
    <div aria-hidden className={clsx("pointer-events-none absolute inset-0 overflow-hidden", className)}>
      {/* Luminance layer. The art is pixel-art, so nearest-neighbour keeps the edges crisp
          instead of smearing them when it scales up on wide viewports. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={lobby.src}
        alt=""
        fetchPriority={priority ? "high" : "auto"}
        decoding="async"
        className={clsx(
          "absolute inset-0 h-full w-full object-cover object-bottom [image-rendering:pixelated]",
          "grayscale contrast-[1.35]",
          hero ? "opacity-[0.55]" : "opacity-[0.28]",
        )}
        style={{
          // Fade all four edges so the art dissolves into the page rather than ending on a line.
          maskImage:
            "radial-gradient(120% 100% at 50% 100%, #000 35%, transparent 100%), linear-gradient(to bottom, transparent, #000 22%, #000 78%, transparent)",
          maskComposite: "intersect",
          WebkitMaskImage:
            "radial-gradient(120% 100% at 50% 100%, #000 35%, transparent 100%), linear-gradient(to bottom, transparent, #000 22%, #000 78%, transparent)",
          WebkitMaskComposite: "source-in",
        }}
      />

      {/* Accent hue driven through the luminance. `color` keeps lightness, replaces hue —
          so the ridgeline stays legible while adopting the live theme. */}
      <span
        className={clsx(
          "absolute inset-0 mix-blend-color",
          hero ? "opacity-90" : "opacity-70",
        )}
        style={{ backgroundColor: "var(--color-accent)" }}
      />

      {/* Seat the art into the page and guarantee the headline's contrast.
          The band is strongest at the bottom, where the copy and buttons sit, and clears toward
          the top so the ridgeline still reads. Using --color-bg means this works in both themes
          without a second code path. */}
      <span
        className="absolute inset-0"
        style={{
          background:
            "linear-gradient(to bottom, color-mix(in srgb, var(--color-bg) 30%, transparent) 0%, color-mix(in srgb, var(--color-bg) 62%, transparent) 45%, color-mix(in srgb, var(--color-bg) 88%, transparent) 78%, var(--color-bg) 100%)",
        }}
      />
    </div>
  );
}
