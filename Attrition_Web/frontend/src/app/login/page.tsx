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
import { GOOGLE_CLIENT_ID } from "@/lib/config";
import { ApiError } from "@/lib/api/client";

const schema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type FormData = z.infer<typeof schema>;

export default function LoginPage() {
  const { login, loginWithGoogle } = useAuth();
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

  const handleGoogle = () => {
    if (!GOOGLE_CLIENT_ID || typeof window === "undefined") return;
    const google = (window as any).google;
    if (!google?.accounts?.id) return;

    google.accounts.id.initialize({
      client_id: GOOGLE_CLIENT_ID,
      callback: async (response: any) => {
        setError("");
        setLoading(true);
        try {
          await loginWithGoogle(response.credential);
          router.push("/");
        } catch (e) {
          if (e instanceof ApiError) {
            setError(tryParseError(e.body) || "Google sign-in failed");
          } else {
            setError("Google sign-in failed");
          }
        } finally {
          setLoading(false);
        }
      },
    });
    google.accounts.id.prompt();
  };

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12">
      <h1 className="font-display text-3xl font-bold text-fg">Sign In</h1>
      <p className="mt-2 text-fg-muted">Welcome back to Attrition</p>

      {error && <div className="mt-4 rounded-lg bg-danger/10 px-4 py-3 text-sm text-danger">{error}</div>}

      <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
        <Input label="Username" type="text" {...register("username")} error={errors.username?.message} />
        <Input label="Password" type="password" {...register("password")} error={errors.password?.message} />

        <div className="flex items-center justify-between">
          <Link href="/forgot-password" className="text-sm text-accent hover:underline">
            Forgot password?
          </Link>
        </div>

        <Button type="submit" loading={loading} className="w-full">
          Sign In
        </Button>
      </form>

      <div className="mt-6 flex items-center gap-3">
        <div className="h-px flex-1 bg-border" />
        <span className="text-xs text-fg-subtle">OR</span>
        <div className="h-px flex-1 bg-border" />
      </div>

      <Button variant="secondary" className="mt-4 w-full" onClick={handleGoogle} disabled={!GOOGLE_CLIENT_ID}>
        Continue with Google
      </Button>

      <p className="mt-6 text-center text-sm text-fg-muted">
        Don&apos;t have an account?{" "}
        <Link href="/register" className="text-accent hover:underline">Sign up</Link>
      </p>

      {GOOGLE_CLIENT_ID && (
        <script src="https://accounts.google.com/gsi/client" async defer />
      )}
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
