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
import { parseApiError } from "@/lib/api/parse-error";
import { PasswordChecklist } from "@/components/password-checklist";
import { passwordSchema } from "@/lib/password-rules";

// Mirrors the server-side policy (Identity.Service validators) so the user gets clear, specific
// feedback BEFORE submitting instead of a generic "registration failed" from the API.
const schema = z.object({
  username: z
    .string()
    .trim()
    .min(3, "Username must be at least 3 characters.")
    .max(20, "Username must be at most 20 characters.")
    .regex(/^[a-z0-9_]+$/, "Use lowercase letters, numbers, and underscores only — no spaces or symbols."),
  email: z.string().trim().min(1, "Email is required.").email("Enter a valid email address."),
  password: passwordSchema,
  confirmPassword: z.string(),
  acceptTerms: z.boolean().refine((v) => v === true, {
    message: "Please accept the Terms of Service and Privacy Policy to continue.",
  }),
}).refine((d) => d.password === d.confirmPassword, {
  message: "Passwords don't match.",
  path: ["confirmPassword"],
});

type FormData = z.infer<typeof schema>;

export default function RegisterPage() {
  const { register: registerUser } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const { register, handleSubmit, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      await registerUser({ username: data.username, email: data.email, password: data.password });
      // New accounts aren't signed in — they must verify their email first.
      router.push("/verify-email?registered=1");
    } catch (e) {
      if (e instanceof ApiError) {
        // 5xx / rate-limit are on us; 4xx are actionable by the user (show the specific reason).
        if (e.status >= 500) setError("Something went wrong on our end. Please try again in a moment.");
        else if (e.status === 429) setError("Too many attempts. Please wait a moment and try again.");
        else setError(parseApiError(e, "Registration failed. Please review your details and try again."));
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
        <h1 className="font-display text-3xl font-bold tracking-tight text-fg">Create Account</h1>
        <p className="mt-2 text-fg-muted">Join the Attrition community.</p>

        {error && (
          <div className="mt-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
          <div>
            <Input label="Username" autoComplete="username" {...register("username")} error={errors.username?.message} />
            {!errors.username && <p className="mt-1 text-xs text-fg-subtle">Lowercase letters, numbers, and underscores.</p>}
          </div>
          <Input label="Email" type="email" autoComplete="email" {...register("email")} error={errors.email?.message} />
          <div>
            <div>
              <Input label="Password" type="password" autoComplete="new-password" {...register("password")} error={errors.password?.message} />
              <PasswordChecklist value={watch("password") ?? ""} />
            </div>
            {!errors.password && <p className="mt-1 text-xs text-fg-subtle">At least 8 characters with an uppercase, lowercase, number, and symbol.</p>}
          </div>
          <Input label="Confirm Password" type="password" autoComplete="new-password" {...register("confirmPassword")} error={errors.confirmPassword?.message} />

          <div>
            <label className="flex items-start gap-2.5 text-sm text-fg-muted">
              <input
                type="checkbox"
                {...register("acceptTerms")}
                className="mt-0.5 h-4 w-4 shrink-0 rounded border-border-strong bg-surface-2 text-accent accent-[var(--color-accent)] focus-visible:outline-2 focus-visible:outline-accent"
              />
              <span>
                I agree to the{" "}
                <Link href="/terms" target="_blank" className="font-medium text-accent transition-opacity hover:opacity-80">Terms of Service</Link>
                {" "}and{" "}
                <Link href="/privacy" target="_blank" className="font-medium text-accent transition-opacity hover:opacity-80">Privacy Policy</Link>.
              </span>
            </label>
            {errors.acceptTerms && <p className="mt-1 text-xs text-danger">{errors.acceptTerms.message}</p>}
          </div>

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
