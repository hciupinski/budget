import { NextResponse } from "next/server";
import { API_BASE_URL } from "@/lib/api";
import { shouldUseSecureCookie } from "@/lib/session-cookie";

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
      "Content-Type": "application/json",
      "X-Login-Email": payload.email?.trim().toLowerCase() ?? ""
    },
    body: JSON.stringify({
      email: payload.email,
      password: payload.password
    }),
    cache: "no-store"
  });

  if (!response.ok) {
    const message =
      response.status === 429
        ? "Too many login attempts. Please wait and try again."
        : "Invalid credentials";
    return NextResponse.json({ error: message }, { status: response.status });
  }

  const data = (await response.json()) as { accessToken: string };
  const nextResponse = NextResponse.json({ ok: true });
  const isSecureRequest = shouldUseSecureCookie(request);

  nextResponse.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: true,
    path: "/"
  });

  nextResponse.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    path: "/"
  });

  nextResponse.cookies.set({
    name: SESSION_COOKIE,
    value: data.accessToken,
    httpOnly: true,
    sameSite: "lax",
    secure: isSecureRequest,
    path: "/",
    maxAge: 60 * 60 * 8
  });

  return nextResponse;
}
