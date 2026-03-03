import { LoginForm } from "@/components/login-form";
import { redirect } from "next/navigation";
import { hasValidOwnerSession } from "@/lib/require-owner-session";

export default async function LoginPage() {
  if (await hasValidOwnerSession()) {
    redirect("/");
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-6 py-10">
      <LoginForm />
    </main>
  );
}
