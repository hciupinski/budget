import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { API_BASE_URL } from "@/lib/api";

type OwnerSession = {
  email: string;
  role: string;
};

export async function requireOwnerSession(): Promise<OwnerSession> {
  const token = cookies().get("budget_session")?.value;

  if (!token) {
    redirect("/login");
  }

  const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`
    },
    cache: "no-store"
  });

  if (!response.ok) {
    redirect("/login");
  }

  return (await response.json()) as OwnerSession;
}

export async function hasValidOwnerSession(): Promise<boolean> {
  const token = cookies().get("budget_session")?.value;

  if (!token) {
    return false;
  }

  const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`
    },
    cache: "no-store"
  });

  return response.ok;
}
