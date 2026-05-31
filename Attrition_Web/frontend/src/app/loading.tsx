import { PageLoader } from "@/components/ui/spinner";

/** Shown during route navigation/suspense — instant feedback instead of a frozen page. */
export default function Loading() {
  return <PageLoader />;
}
