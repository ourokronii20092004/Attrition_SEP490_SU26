"use client";

import { useState } from "react";
import Link from "next/link";
import {
  Download, HardDrive, Monitor, FileArchive, Gamepad2, MessageSquare, Check, Copy,
} from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { LobbyScene } from "@/components/lobby-scene";
import { GAME_BUILD, GAME_BUILD_AVAILABLE, OLDER_BUILDS, type GameBuild } from "@/lib/game-build";

const dateFmt = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric" });
const formatReleased = (iso: string) => dateFmt.format(new Date(`${iso}T00:00:00Z`));

export default function DownloadPage() {
  return (
    <div>
      {/* ─── The build itself is the hero: one obvious action, over the world you're downloading ─── */}
      <section className="relative overflow-hidden border-b border-border">
        <LobbyScene intensity="hero" priority />
        <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

        <div className="relative mx-auto w-full max-w-3xl px-5 pb-20 pt-24 text-center sm:px-8 sm:pb-24 sm:pt-28">
          <h1 className="animate-rise-in font-display text-5xl font-extrabold leading-[0.98] tracking-tight text-balance text-fg sm:text-6xl">
            Download Attrition
          </h1>
          <p className="animate-rise-in mx-auto mt-5 max-w-xl text-lg leading-relaxed text-fg-muted [animation-delay:180ms]">
            Free, for Windows. Bring a friend — co-op runs the whole campaign.
          </p>

          <div className="animate-rise-in mt-9 [animation-delay:300ms]">
            {GAME_BUILD_AVAILABLE ? (
              // A styled anchor, not a Button: the browser must treat this as a file download,
              // and `download` only works on an anchor.
              <a
                href={GAME_BUILD.url}
                download
                className="group inline-flex items-center gap-2.5 rounded-md bg-accent px-8 py-4 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_45%,transparent)] transition-[transform,box-shadow,filter] duration-200 hover:shadow-[var(--shadow-glow)] hover:brightness-105 active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                <Download size={18} className="transition-transform duration-200 group-hover:-translate-y-0.5" aria-hidden />
                Download v{GAME_BUILD.version}
              </a>
            ) : (
              <Button size="lg" disabled>
                <Download size={18} className="mr-2" aria-hidden />
                Build being prepared
              </Button>
            )}
            <p className="mt-4 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-muted">
              {GAME_BUILD.channel} · {GAME_BUILD.sizeLabel} · Windows 64-bit
            </p>
          </div>
        </div>
      </section>

      <PageShell size="md">
        {/* ─── All builds, newest first ─── */}
        <section>
          <div className="flex flex-wrap items-end justify-between gap-3 border-b border-border pb-4">
            <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">All builds</h2>
            <p className="text-xs text-fg-subtle">
              Older builds stay up if you&rsquo;re mid-run on one.
            </p>
          </div>

          <ul className="mt-6 space-y-3">
            <BuildRow build={GAME_BUILD} current />
            {OLDER_BUILDS.map((b) => (
              <BuildRow key={b.version} build={b} />
            ))}
          </ul>
        </section>

        {/* ─── Before you play ─── */}
        <section className="mt-14">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">Before you play</h2>
          <div className="mt-6 grid gap-4 sm:grid-cols-2">
            <Fact icon={Monitor} title="Windows only">
              Every build ships a Windows 64-bit player. There is no macOS or Linux build yet.
            </Fact>
            <Fact icon={FileArchive} title="Extract it first">
              {GAME_BUILD.archive === ".zip" ? (
                <>
                  It&rsquo;s a .zip, which Windows opens on its own — right-click, Extract All, then
                  run <code className="font-mono text-xs">{GAME_BUILD.executable}</code>.
                </>
              ) : (
                <>
                  It&rsquo;s a {GAME_BUILD.archive} archive, which Windows can&rsquo;t open on its own —
                  use 7-Zip or WinRAR, then run{" "}
                  <code className="font-mono text-xs">{GAME_BUILD.executable}</code>.
                </>
              )}
            </Fact>
            <Fact icon={HardDrive} title="Keep the folder together">
              Launch from inside the extracted folder. Moving the executable away from its data
              folder stops the game from starting.
            </Fact>
            <Fact icon={Gamepad2} title="Online features need an account">
              Sign in with the same account you use here to sync your characters and progress.
            </Fact>
          </div>
        </section>

        {/* ─── Bugs ─── */}
        <section className="mt-14 rounded-card border border-border bg-surface-2/40 p-6">
          <h2 className="font-display text-lg font-semibold text-fg">Hit a bug?</h2>
          <p className="mt-2 text-sm leading-relaxed text-fg-muted">
            The forum is the fastest way to get it fixed — include your build version and what you
            were doing when it happened.
          </p>
          <div className="mt-5 flex flex-wrap gap-2">
            <Link href="/forum">
              <Button variant="secondary" size="sm">
                <MessageSquare size={15} className="mr-1.5" aria-hidden /> Report it on the forum
              </Button>
            </Link>
            <Link href="/story">
              <Button variant="ghost" size="sm">Read the story so far</Button>
            </Link>
          </div>
        </section>
      </PageShell>
    </div>
  );
}

/**
 * One build in the list. The current release is visually promoted and keeps a filled button;
 * superseded builds stay downloadable but recede, so "which one do I want" is answered by
 * looking rather than reading.
 */
function BuildRow({ build, current = false }: { build: GameBuild; current?: boolean }) {
  const available = build.url.length > 0;

  return (
    <li
      className={
        current
          ? "rounded-card border border-accent/40 bg-surface p-5 shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_18%,transparent)]"
          : "rounded-card border border-border bg-surface p-5 transition-colors hover:border-border-strong"
      }
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-display text-lg font-bold tracking-tight text-fg">
              Attrition v{build.version}
            </h3>
            <span
              className={
                current
                  ? "rounded-full bg-accent px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-accent-fg"
                  : "rounded-full border border-border bg-surface-2 px-2.5 py-0.5 text-[11px] font-medium uppercase tracking-wider text-fg-muted"
              }
            >
              {current ? "Latest" : build.channel}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-fg-muted">
            {build.sizeLabel} · {build.archive} · {build.platform}
          </p>
          <p className="mt-0.5 text-xs text-fg-subtle">
            Released {formatReleased(build.released)} · {build.executable}
          </p>
        </div>

        {available ? (
          <a
            href={build.url}
            download
            aria-label={`Download Attrition version ${build.version}`}
            className={
              current
                ? "group inline-flex shrink-0 items-center gap-2 rounded-md bg-accent px-5 py-2.5 text-sm font-semibold text-accent-fg transition-[filter,box-shadow] duration-200 hover:brightness-105 hover:shadow-[var(--shadow-glow)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                : "group inline-flex shrink-0 items-center gap-2 rounded-md border border-border px-4 py-2.5 text-sm font-medium text-fg-muted transition-colors hover:border-accent/50 hover:text-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            }
          >
            <Download size={15} className="transition-transform duration-200 group-hover:-translate-y-0.5" aria-hidden />
            Download
          </a>
        ) : (
          <span className="shrink-0 rounded-md border border-border px-4 py-2.5 text-sm text-fg-subtle">
            Unavailable
          </span>
        )}
      </div>

      <ChecksumRow sha256={build.sha256} archive={build.archive} />
    </li>
  );
}

/**
 * Collapsed checksum with copy-to-clipboard. Verification matters to the handful of people whose
 * download was interrupted and to nobody else, so it stays out of the way until asked for.
 */
function ChecksumRow({ sha256, archive }: { sha256: string; archive: string }) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(sha256);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard can be blocked (insecure origin, denied permission). The hash is on screen
      // and selectable, so silently leaving the button unconfirmed is the honest outcome.
    }
  };

  return (
    <div className="mt-4 border-t border-border pt-3">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="text-xs font-medium text-fg-subtle transition-colors hover:text-fg"
      >
        {open ? "Hide checksum" : "Verify this download"}
      </button>

      {open && (
        <div className="mt-3">
          <p className="text-xs leading-relaxed text-fg-muted">
            In PowerShell, run{" "}
            <code className="font-mono">Get-FileHash .\{"<file>"}{archive}</code> and check the
            result matches:
          </p>
          <div className="mt-2 flex items-start gap-2">
            <code className="min-w-0 flex-1 break-all rounded-md bg-surface-2 p-2.5 font-mono text-[11px] leading-relaxed text-fg-muted">
              {sha256}
            </code>
            <button
              type="button"
              onClick={copy}
              aria-label="Copy checksum"
              className="shrink-0 rounded-md border border-border p-2 text-fg-subtle transition-colors hover:border-accent/50 hover:text-accent"
            >
              {copied ? <Check size={14} className="text-accent" aria-hidden /> : <Copy size={14} aria-hidden />}
            </button>
          </div>
          {copied && <p className="mt-1.5 text-[11px] text-accent">Copied to clipboard.</p>}
        </div>
      )}
    </div>
  );
}

function Fact({ icon: Icon, title, children }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Card className="p-5">
      <h3 className="flex items-center gap-2 font-medium text-fg">
        <Icon size={16} className="shrink-0 text-accent" aria-hidden />
        {title}
      </h3>
      <p className="mt-2 text-sm leading-relaxed text-fg-muted">{children}</p>
    </Card>
  );
}
