import { NextResponse } from "next/server";

export function GET() {
  return NextResponse.json({
    status: "ok",
    service: "web",
    utcTime: new Date().toISOString()
  });
}
