import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { API_BASE_URL } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type OwnerResponse = {
  email: string;
  role: string;
};

async function loadOwner(token: string): Promise<OwnerResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`
    },
    cache: "no-store"
  });

  if (!response.ok) {
    throw new Error("Unable to load owner profile");
  }

  return response.json() as Promise<OwnerResponse>;
}

export default async function HomePage() {
  const token = cookies().get("budget_session")?.value;

  if (!token) {
    redirect("/login");
  }

  const owner = await loadOwner(token);

  return (
    <main className="mx-auto flex min-h-screen max-w-4xl items-center px-6 py-10">
      <Card className="w-full">
        <CardHeader>
          <CardTitle>Budget Foundation Ready</CardTitle>
          <CardDescription>
            Signed in as {owner.email} ({owner.role}).
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Epic 1 baseline is active: auth-gated app, secured API, worker heartbeat, and health endpoints.
          </p>
          <form action="/api/auth/logout" method="post">
            <Button type="submit" variant="outline">
              Sign out
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
