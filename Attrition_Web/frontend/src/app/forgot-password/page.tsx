"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { authApi } from "@/lib/api/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/client";

const schema = z.object({
  email: z.string().email("Invalid email"),
});

type FormData = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      await authApi.forgotPassword(data);
      setSent(true);
    } catch (e) {
      if (e instanceof ApiError) {
        setError("Failed to send reset email");
      } else {
        setError("Something went wrong");
      }
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <div className="mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
        <div className="glass rounded-2xl p-6 text-center shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Check Your Email</h1>
          <p className="mt-4 text-fg-muted">
            If an account with that email exists, we&apos;ve sent a password reset link.
          </p>
          <Link href="/login" className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">
            Back to sign in
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="relative mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
      <span aria-hidden className="pointer-events-none absolute left-1/2 top-10 h-56 w-56 -translate-x-1/2 rounded-full bg-accent/15 blur-[90px]" />
      <div className="glass relative rounded-2xl p-6 shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Forgot Password</h1>
        <p className="mt-2 text-fg-muted">Enter your email and we&apos;ll send a reset link.</p>

        {error && (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <Input label="Email" type="email" autoComplete="email" {...register("email")} error={errors.email?.message} />
          <Button type="submit" loading={loading} className="w-full">
            Send Reset Link
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-fg-muted">
          <Link href="/login" className="font-medium text-accent transition-opacity hover:opacity-80">Back to sign in</Link>
        </p>
      </div>
    </div>
  );
}
