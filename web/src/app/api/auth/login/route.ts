import { NextResponse } from "next/server";
import { API_BASE_URL } from "@/lib/api";

const SESSION_COOKIE = "budget_session";

export async function POST(request: Request) {
  let payload: { email?: string; password?: string };

  try {
    payload = (await request.json()) as { email?: string; password?: string };
  } catch {
    return NextResponse.json({ error: "Invalid request payload" }, { status: 400 });
  }

  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      email: payload.email,
      password: payload.password
    }),
    cache: "no-store"
  });

  if (!response.ok) {
    return NextResponse.json({ error: "Invalid credentials" }, { status: response.status });
  }

  const data = (await response.json()) as { accessToken: string };
  const nextResponse = NextResponse.json({ ok: true });

  nextResponse.cookies.set({
    name: SESSION_COOKIE,
    value: data.accessToken,
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: 60 * 60 * 8
  });

  return nextResponse;
}
