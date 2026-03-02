import { NextResponse } from "next/server";

const SESSION_COOKIE = "budget_session";

export function POST(request: Request) {
  const response = NextResponse.redirect(new URL("/login", request.url), { status: 303 });

  response.cookies.set({
    name: SESSION_COOKIE,
    value: "",
    maxAge: 0,
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/"
  });

  return response;
}
