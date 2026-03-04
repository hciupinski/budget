import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET() {
  return proxyBudgetApi("/api/budget/assets/investments", {
    method: "GET"
  });
}
