import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(request: Request, context: { params: { year: string; month: string } }) {
  const url = new URL(request.url);
  const status = url.searchParams.get("status");
  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";

  return proxyBudgetApi(`/api/budget/months/${context.params.year}/${context.params.month}${suffix}`, {
    method: "GET"
  });
}
