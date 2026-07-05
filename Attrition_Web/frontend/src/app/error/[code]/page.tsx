import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ErrorScreen, ERROR_COPY } from "@/components/error-screen";

/**
 * Visitable error pages for any status we might surface (e.g. /error/403, /error/503). Used as a
 * redirect target for fatal, code-specific failures, and handy for testing the funny error screens.
 * Unknown codes fall back to a generic message.
 */
export default async function ErrorCodePage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const n = parseInt(code, 10);
  const copy = ERROR_COPY[n] ?? {
    title: "Something Broke",
    message: "An unknown error crept out of the Corruption. We logged it; you saw nothing.",
  };
  return (
    <ErrorScreen code={Number.isFinite(n) ? n : "ERR"} title={copy.title} message={copy.message}>
      <Link href="/"><Button>Back to home</Button></Link>
    </ErrorScreen>
  );
}
