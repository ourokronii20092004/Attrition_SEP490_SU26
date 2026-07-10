import { PageLoader } from "@/components/ui/spinner";

/** Route navigation/suspense fallback — a plain spinner. The branded/funny loading screen is
 * reserved for login/logout transitions (see AuthTransition). */
export default function Loading() {
  return <PageLoader />;
}
