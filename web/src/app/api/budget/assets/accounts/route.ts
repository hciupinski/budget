import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST(request: Request) {
  const body = await request.text();

  return proxyBudgetApi("/api/budget/assets/accounts", {
    method: "POST",
    body
  });
}
