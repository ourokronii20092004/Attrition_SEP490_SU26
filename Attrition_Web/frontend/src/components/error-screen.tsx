import type { ReactNode } from "react";

/**
 * Static, in-world funny copy per HTTP status. Keep the humor dry and on-theme (the "Corruption"
 * archive) — these render as full-page error screens, so they should land, not grovel.
 */
export const ERROR_COPY: Record<number, { title: string; message: string }> = {
  400: { title: "Bad Request", message: "The Corruption garbled that request past all reading. Try phrasing it like you mean it." },
  401: { title: "Not Signed In", message: "You're not on the list. The Archive doesn't know your face yet — sign in and try again." },
  403: { title: "Forbidden", message: "This door is sealed, and you are very much not cleared for whatever's breathing behind it." },
  404: { title: "Lost in the Void", message: "This path dissolved into ash. Whatever was here, the Corruption already took it." },
  408: { title: "Request Timeout", message: "You hesitated, and the moment rotted before it ever reached us." },
  418: { title: "I'm a Teapot", message: "You asked the archive to brew coffee. It is, regrettably, a teapot." },
  429: { title: "Too Many Requests", message: "Slow down, survivor — even the Corruption stops to breathe now and then." },
  500: { title: "Internal Server Error", message: "The server caught the Corruption, and it is not taking it well. We're on it." },
  502: { title: "Bad Gateway", message: "The messenger came back with a mouthful of static and no answer at all." },
  503: { title: "Service Unavailable", message: "The Archive is overrun for the moment. Try again once the halls have cleared." },
  504: { title: "Gateway Timeout", message: "The deeper strata went quiet and never answered the knock." },
};

function defaultKicker(code: string | number): string {
  const n = typeof code === "number" ? code : parseInt(String(code), 10);
  if (!Number.isFinite(n)) return "Signal lost";
  if (n >= 500) return "Server error";
  if (n >= 400) return "Client error";
  return "Signal lost";
}

/**
 * Full-page error screen: a big status code, an in-world title + message, and caller-supplied
 * actions. Presentational and hook-free, so it works in both server (not-found) and client
 * (error boundary) components. Themed to match the site's Corruption atmosphere.
 */
export function ErrorScreen({ code, title, message, kicker, children }: {
  code: string | number;
  title: string;
  message: string;
  kicker?: string;
  children?: ReactNode;
}) {
  return (
    <div className="relative mx-auto flex min-h-[70vh] max-w-4xl flex-col items-center justify-center px-5 py-20 text-center">
      <span aria-hidden className="pointer-events-none absolute left-1/2 top-1/3 h-72 w-72 -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent/12 blur-[120px]" />
      <span aria-hidden className="pointer-events-none absolute inset-x-8 top-1/4 h-px bg-gradient-to-r from-transparent via-accent/25 to-transparent" />

      <p className="relative font-mono text-[11px] uppercase tracking-[0.35em] text-accent">{kicker ?? defaultKicker(code)}</p>
      <p aria-hidden className="relative mt-3 font-display text-[6rem] font-black leading-none tracking-tighter text-fg sm:text-[8rem]">
        {code}
      </p>
      <h1 className="mt-2 font-display text-2xl font-bold tracking-tight text-balance text-fg sm:text-4xl">{title}</h1>
      <p className="mt-4 max-w-2xl text-lg leading-relaxed text-fg-muted">{message}</p>
      {children && <div className="mt-8 flex flex-wrap items-center justify-center gap-3">{children}</div>}
    </div>
  );
}
