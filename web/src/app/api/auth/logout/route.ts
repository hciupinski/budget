import { NextResponse } from "next/server";
import { resolveRedirectOrigin, shouldUseSecureCookie } from "@/lib/session-cookie";

const SESSION_COOKIE = "budget_session";

export function POST(request: Request) {
  const redirectOrigin = resolveRedirectOrigin(request);
  const response = NextResponse.redirect(new URL("/login", redirectOrigin), { status: 303 });
  const isSecureRequest = shouldUseSecureCookie(request);

  response.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: isSecureRequest,
    path: "/"
  });

  return response;
}
