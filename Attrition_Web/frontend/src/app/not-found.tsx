import Link from "next/link";
import { Compass } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function NotFound() {
  return (
    <div className="mx-auto flex min-h-[60vh] max-w-2xl items-center px-5">
      <EmptyState
        icon={Compass}
        title="Page not found"
        description="This path leads nowhere — the page may have been removed, or the link is wrong."
        className="w-full"
        action={
          <Link href="/">
            <Button>Back to home</Button>
          </Link>
        }
      />
    </div>
  );
}
