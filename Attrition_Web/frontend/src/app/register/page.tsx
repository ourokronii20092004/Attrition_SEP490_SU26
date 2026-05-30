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
  username: z.string().min(3, "At least 3 characters").max(20, "Max 20 characters"),
  email: z.string().email("Invalid email"),
  password: z.string().min(6, "At least 6 characters"),
  confirmPassword: z.string(),
}).refine((d) => d.password === d.confirmPassword, {
  message: "Passwords don't match",
  path: ["confirmPassword"],
});

type FormData = z.infer<typeof schema>;

export default function RegisterPage() {
  const { register: registerUser } = useAuth();
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
      await registerUser({ username: data.username, email: data.email, password: data.password });
      router.push("/");
    } catch (e) {
      if (e instanceof ApiError) {
        setError(tryParseError(e.body) || "Registration failed");
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
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Create Account</h1>
        <p className="mt-2 text-fg-muted">Join the Attrition community.</p>

        {error && (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <Input label="Username" autoComplete="username" {...register("username")} error={errors.username?.message} />
          <Input label="Email" type="email" autoComplete="email" {...register("email")} error={errors.email?.message} />
          <Input label="Password" type="password" autoComplete="new-password" {...register("password")} error={errors.password?.message} />
          <Input label="Confirm Password" type="password" autoComplete="new-password" {...register("confirmPassword")} error={errors.confirmPassword?.message} />

          <Button type="submit" loading={loading} className="w-full">
            Create Account
          </Button>
        </form>

        <GoogleButton label="Sign up with Google" />

        <p className="mt-6 text-center text-sm text-fg-muted">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-accent transition-opacity hover:opacity-80">Sign in</Link>
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
