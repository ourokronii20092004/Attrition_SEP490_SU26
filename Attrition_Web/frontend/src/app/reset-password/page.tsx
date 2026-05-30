"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { authApi } from "@/lib/api/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { ApiError } from "@/lib/api/client";

const schema = z.object({
  newPassword: z.string().min(6, "At least 6 characters"),
  confirmPassword: z.string(),
}).refine((d) => d.newPassword === d.confirmPassword, {
  message: "Passwords don't match",
  path: ["confirmPassword"],
});

type FormData = z.infer<typeof schema>;

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<PageLoader />}>
      <ResetPasswordContent />
    </Suspense>
  );
}

function ResetPasswordContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    if (!token) {
      setError("Invalid reset link");
      return;
    }
    setError("");
    setLoading(true);
    try {
      await authApi.resetPassword({ token, newPassword: data.newPassword });
      setSuccess(true);
    } catch (e) {
      if (e instanceof ApiError) {
        setError("Reset failed. The link may have expired.");
      } else {
        setError("Something went wrong");
      }
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <div className="mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
        <div className="glass rounded-2xl p-6 text-center shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Password Reset</h1>
          <p className="mt-4 text-fg-muted">Your password has been updated successfully.</p>
          <Link href="/login" className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">
            Sign in with your new password
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="relative mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
      <span aria-hidden className="pointer-events-none absolute left-1/2 top-10 h-56 w-56 -translate-x-1/2 rounded-full bg-accent/15 blur-[90px]" />
      <div className="glass relative rounded-2xl p-6 shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Reset Password</h1>
        <p className="mt-2 text-fg-muted">Choose a new password.</p>

        {error && (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <Input label="New Password" type="password" autoComplete="new-password" {...register("newPassword")} error={errors.newPassword?.message} />
          <Input label="Confirm Password" type="password" autoComplete="new-password" {...register("confirmPassword")} error={errors.confirmPassword?.message} />
          <Button type="submit" loading={loading} className="w-full">
            Reset Password
          </Button>
        </form>
      </div>
    </div>
  );
}
