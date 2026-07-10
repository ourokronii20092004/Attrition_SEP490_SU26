"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ErrorScreen } from "@/components/error-screen";

// There's no build yet — the "download" is a running gag. A fresh line is picked on each visit.
const NO_BUILD_JOKES = [
  "No build yet — Ren is still learning which end of the sword is the pointy one.",
  "The game escaped containment and is loose somewhere in the Corruption.",
  "We traded the final build to a merchant for a healing potion. Rough deal.",
  "Still compiling. Estimated time remaining: \u201cyes.\u201d",
  "The .exe unionized. It is currently on strike for better working conditions.",
  "Download unavailable: QA is being held hostage by the Bestiary.",
  "Coming soon\u2122. The \u2122 is doing an enormous amount of heavy lifting.",
  "The build server wandered into the Archive and hasn't come back out.",
  "404: Game not found. Have you considered, as an alternative, going outside?",
  "We mined the install files for Bitcoin. It did not go the way we hoped.",
  "The devs are currently gaslighting the compiler. Please hold.",
  "It's done when Iris says it's done, and Iris isn't talking.",
  "The final boss ghosted us mid-negotiation. We're as surprised as you are.",
  "Downloading more RAM to fit the download. Circular, we know.",
];

export default function DownloadPage() {
  // Start from a stable line for SSR, then swap to a random one on mount (no hydration mismatch).
  const [msg, setMsg] = useState(NO_BUILD_JOKES[0]);
  useEffect(() => {
    setMsg(NO_BUILD_JOKES[Math.floor(Math.random() * NO_BUILD_JOKES.length)]);
  }, []);

  return (
    <ErrorScreen code="501" kicker="No build yet" title="The game isn't out (yet)" message={msg}>
      <Link href="/"><Button>Back to home</Button></Link>
      <Link href="/forum"><Button variant="secondary">Pester the devs on the forum</Button></Link>
    </ErrorScreen>
  );
}
