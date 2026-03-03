import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET() {
  return proxyBudgetApi("/api/budget/settings/sections", {
    method: "GET"
  });
}

export async function PUT(request: Request) {
  const body = await request.text();

  return proxyBudgetApi("/api/budget/settings/sections", {
    method: "PUT",
    body
  });
}
