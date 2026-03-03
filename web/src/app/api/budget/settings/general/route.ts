import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET() {
  return proxyBudgetApi("/api/budget/settings/general", {
    method: "GET"
  });
}

export async function PUT(request: Request) {
  const body = await request.text();

  return proxyBudgetApi("/api/budget/settings/general", {
    method: "PUT",
    body
  });
}
