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
      <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Password Reset</h1>
        <p className="mt-4 text-fg-muted">Your password has been updated successfully.</p>
        <Link href="/login" className="mt-6 text-accent hover:underline">Sign in with your new password</Link>
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12">
      <h1 className="font-display text-3xl font-bold text-fg">Reset Password</h1>
      <p className="mt-2 text-fg-muted">Choose a new password</p>

      {error && <div className="mt-4 rounded-lg bg-danger/10 px-4 py-3 text-sm text-danger">{error}</div>}

      <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
        <Input label="New Password" type="password" {...register("newPassword")} error={errors.newPassword?.message} />
        <Input label="Confirm Password" type="password" {...register("confirmPassword")} error={errors.confirmPassword?.message} />
        <Button type="submit" loading={loading} className="w-full">
          Reset Password
        </Button>
      </form>
    </div>
  );
}
