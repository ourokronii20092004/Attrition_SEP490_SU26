"use client";

import { useAuth } from "@/contexts/AuthContext";
import { useRouter } from "next/navigation";

/**
 * Client component for the "New Thread" button.
 * If authenticated → navigates to /forum/new
 * If not → redirects to /login?redirect=forum/new
 */
export function NewThreadButton() {
  const { isAuthenticated } = useAuth();
  const router = useRouter();

  const handleClick = () => {
    if (isAuthenticated) {
      router.push("/forum/new");
    } else {
      router.push("/login?redirect=forum/new");
    }
  };

  return (
    <button onClick={handleClick} className="btn btn-primary btn-md">
      New Thread
    </button>
  );
}
