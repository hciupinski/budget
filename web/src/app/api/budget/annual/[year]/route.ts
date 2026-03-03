import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(_: Request, context: { params: Promise<{ year: string }> }) {
  const { year } = await context.params;

  return proxyBudgetApi(`/api/budget/annual/${year}`, { method: "GET" });
}

export async function PUT(request: Request, context: { params: Promise<{ year: string }> }) {
  const { year } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/annual/${year}`, {
    method: "PUT",
    body
  });
}
