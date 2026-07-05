import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ErrorScreen, ERROR_COPY } from "@/components/error-screen";

export default function NotFound() {
  return (
    <ErrorScreen code={404} title={ERROR_COPY[404].title} message={ERROR_COPY[404].message}>
      <Link href="/"><Button>Back to home</Button></Link>
      <Link href="/wiki"><Button variant="secondary">Search the archive</Button></Link>
    </ErrorScreen>
  );
}
