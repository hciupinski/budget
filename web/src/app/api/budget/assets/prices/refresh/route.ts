import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST() {
  return proxyBudgetApi("/api/budget/assets/prices/refresh", {
    method: "POST"
  });
}
