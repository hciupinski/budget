import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const suffix = url.search ? url.search : "";

  return proxyBudgetApi(`/api/budget/assets/investments${suffix}`, {
    method: "GET"
  });
}
