"use client";

import { useState, useEffect } from "react";
import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { GoogleButton } from "@/components/google-button";
import { ApiError } from "@/lib/api/client";
import { parseApiError } from "@/lib/api/parse-error";

const schema = z.object({
  username: z.string().trim().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type FormData = z.infer<typeof schema>;

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [retryIn, setRetryIn] = useState(0); // rate-limit countdown (seconds)

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  // Tick the rate-limit countdown down to zero.
  useEffect(() => {
    if (retryIn <= 0) return;
    const id = setInterval(() => setRetryIn((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, [retryIn]);

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      const loggedIn = await login(data);
      const redirect = searchParams.get("redirect");
      // Admins are confined to the admin panel; everyone else honors ?redirect or lands home.
      if (loggedIn?.role === "Admin") router.push("/admin");
      else router.push(redirect || "/");
    } catch (e) {
      if (e instanceof ApiError && e.status === 429) {
        // Rate limited — show a countdown, not the generic credentials error.
        setRetryIn(e.retryAfter && e.retryAfter > 0 ? e.retryAfter : 60);
      } else if (e instanceof ApiError) {
        // 5xx is on us; 4xx carries an actionable reason (bad credentials, unverified email, …).
        if (e.status >= 500) setError("Something went wrong on our end. Please try again in a moment.");
        else setError(parseApiError(e, "Invalid username or password."));
      } else {
        setError("We couldn't reach the server. Check your internet connection and try again.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
      <span aria-hidden className="pointer-events-none absolute left-1/2 top-10 h-56 w-56 -translate-x-1/2 rounded-full bg-accent/15 blur-[90px]" />
      <div className="glass relative rounded-2xl p-6 shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Sign In</h1>
        <p className="mt-2 text-fg-muted">Welcome back to Attrition.</p>

        {retryIn > 0 ? (
          <div className="mt-4 rounded-lg border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning" role="alert">
            Too many attempts. Try again in {retryIn}s.
          </div>
        ) : error ? (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        ) : null}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
          <Input label="Username" type="text" autoComplete="username" {...register("username")} error={errors.username?.message} />
          <Input label="Password" type="password" autoComplete="current-password" {...register("password")} error={errors.password?.message} />

          <div className="flex items-center justify-end">
            <Link href="/forgot-password" className="text-sm text-accent transition-opacity hover:opacity-80">
              Forgot password?
            </Link>
          </div>

          <Button type="submit" loading={loading} disabled={retryIn > 0} className="w-full">
            Sign In
          </Button>
        </form>

        <GoogleButton />

        <p className="mt-6 text-center text-sm text-fg-muted">
          Don&apos;t have an account?{" "}
          <Link href="/register" className="font-medium text-accent transition-opacity hover:opacity-80">Sign up</Link>
        </p>
      </div>
    </div>
  );
}
