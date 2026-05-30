"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { GoogleButton } from "@/components/google-button";
import { ApiError } from "@/lib/api/client";

const schema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type FormData = z.infer<typeof schema>;

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      await login(data);
      router.push("/");
    } catch (e) {
      if (e instanceof ApiError) {
        const body = tryParseError(e.body);
        setError(body || "Invalid username or password");
      } else {
        setError("Something went wrong");
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

        {error && (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <Input label="Username" type="text" autoComplete="username" {...register("username")} error={errors.username?.message} />
          <Input label="Password" type="password" autoComplete="current-password" {...register("password")} error={errors.password?.message} />

          <div className="flex items-center justify-end">
            <Link href="/forgot-password" className="text-sm text-accent transition-opacity hover:opacity-80">
              Forgot password?
            </Link>
          </div>

          <Button type="submit" loading={loading} className="w-full">
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

function tryParseError(body: string): string {
  try {
    const json = JSON.parse(body);
    return json.error || json.message || "";
  } catch {
    return body;
  }
}
