"use client";

import Link from "next/link";
import { Download, HardDrive, Monitor, FileArchive, ShieldCheck, MessageSquare, Gamepad2 } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { GAME_BUILD, GAME_BUILD_AVAILABLE } from "@/lib/game-build";

export default function DownloadPage() {
  return (
    <PageShell size="md">
      <PageTitle
        eyebrow={`${GAME_BUILD.channel} · v${GAME_BUILD.version}`}
        description="The open beta build of Attrition for Windows. Bring a friend — co-op runs through the whole campaign."
      >
        Download Attrition
      </PageTitle>

      <Card className="p-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="min-w-0">
            <h2 className="font-display text-xl font-semibold text-fg">
              Attrition v{GAME_BUILD.version}
            </h2>
            <p className="mt-1 text-sm text-fg-muted">
              {GAME_BUILD.platform} · {GAME_BUILD.sizeLabel} · {GAME_BUILD.archive} archive
            </p>
          </div>

          {GAME_BUILD_AVAILABLE ? (
            // A styled anchor, not a Button: the browser must handle this as a file download,
            // and `download` only works on an anchor.
            <a
              href={GAME_BUILD.url}
              download
              className="group inline-flex shrink-0 items-center gap-2 rounded-md bg-accent px-7 py-3.5 text-sm font-semibold tracking-wide text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_45%,transparent)] transition-[transform,box-shadow,filter] duration-200 hover:shadow-[var(--shadow-glow)] hover:brightness-105 active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              <Download size={18} className="transition-transform duration-200 group-hover:-translate-y-0.5" aria-hidden />
              Download for Windows
            </a>
          ) : (
            <Button size="lg" disabled>
              <Download size={18} className="mr-2" aria-hidden />
              Build being prepared
            </Button>
          )}
        </div>
      </Card>

      <section className="mt-8">
        <h2 className="font-display text-lg font-semibold text-fg">Before you play</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <Fact icon={Monitor} title="Windows only">
            This build ships a Windows 64-bit player. There is no macOS or Linux build yet.
          </Fact>
          <Fact icon={FileArchive} title="You'll need an extractor">
            It's a {GAME_BUILD.archive} archive, which Windows can't open on its own — use 7-Zip
            or WinRAR, then run <code className="font-mono text-xs">{GAME_BUILD.executable}</code>.
          </Fact>
          <Fact icon={HardDrive} title="Keep the folder together">
            Extract the whole archive and launch from inside it. Moving the executable away from
            its data folder stops the game from starting.
          </Fact>
          <Fact icon={Gamepad2} title="Online features need an account">
            Sign in with the same account you use here to sync your characters and progress.
          </Fact>
        </div>
      </section>

      <section className="mt-8">
        <h2 className="font-display text-lg font-semibold text-fg">Verify your download</h2>
        <Card className="mt-4 p-5">
          <p className="flex items-start gap-2 text-sm text-fg-muted">
            <ShieldCheck size={16} className="mt-0.5 shrink-0 text-accent" aria-hidden />
            <span>
              Optional, but worth doing if the download was interrupted. In PowerShell, run{" "}
              <code className="font-mono text-xs">Get-FileHash .\{"<file>"}.rar</code> and check the
              result matches:
            </span>
          </p>
          <p className="mt-3 break-all rounded-lg bg-surface-2 p-3 font-mono text-xs text-fg-muted">
            {GAME_BUILD.sha256}
          </p>
        </Card>
      </section>

      <section className="mt-8 rounded-card border border-border bg-surface-2/40 p-5">
        <h2 className="font-display text-lg font-semibold text-fg">It&apos;s a beta</h2>
        <p className="mt-2 text-sm text-fg-muted">
          Expect rough edges and the occasional bug. If something breaks, telling us on the forum
          is the fastest way to get it fixed.
        </p>
        <div className="mt-4 flex flex-wrap gap-2">
          <Link href="/forum"><Button variant="secondary" size="sm">
            <MessageSquare size={15} className="mr-1.5" aria-hidden /> Report it on the forum
          </Button></Link>
          <Link href="/story"><Button variant="ghost" size="sm">Read the story so far</Button></Link>
        </div>
      </section>
    </PageShell>
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
      <p className="mt-2 text-sm text-fg-muted">{children}</p>
    </Card>
  );
}
