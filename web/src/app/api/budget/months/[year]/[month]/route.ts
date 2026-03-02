import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(
  request: Request,
  context: { params: Promise<{ year: string; month: string }> }
) {
  const { year, month } = await context.params;
  const url = new URL(request.url);
  const status = url.searchParams.get("status");
  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";

  return proxyBudgetApi(`/api/budget/months/${year}/${month}${suffix}`, {
    method: "GET"
  });
}
