import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { API_BASE_URL } from "@/lib/api";

const SESSION_COOKIE = "budget_session";

function clearSessionCookies(response: NextResponse): void {
  response.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: true,
    path: "/"
  });

  response.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    path: "/"
  });
}

export async function proxyBudgetApi(path: string, init: RequestInit = {}): Promise<NextResponse> {
  const token = cookies().get(SESSION_COOKIE)?.value;

  if (!token) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    cache: "no-store"
  });

  const text = await response.text();
  const contentType = response.headers.get("content-type") ?? "application/json";

  const nextResponse = new NextResponse(text, {
    status: response.status,
    headers: {
      "content-type": contentType
    }
  });

  if (response.status === 401) {
    clearSessionCookies(nextResponse);
  }

  return nextResponse;
}
