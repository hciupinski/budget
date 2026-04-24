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
  const cookieStore = await cookies();
  const token = cookieStore.get(SESSION_COOKIE)?.value;

  if (!token) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);

  if (typeof init.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    cache: "no-store"
  });

  const responseHeaders = new Headers();
  const contentType = response.headers.get("content-type");
  const contentDisposition = response.headers.get("content-disposition");
  if (contentType) {
    responseHeaders.set("content-type", contentType);
  }
  if (contentDisposition) {
    responseHeaders.set("content-disposition", contentDisposition);
  }

  const nextResponse = new NextResponse(response.body, {
    status: response.status,
    headers: responseHeaders
  });

  if (response.status === 401) {
    clearSessionCookies(nextResponse);
  }

  return nextResponse;
}
