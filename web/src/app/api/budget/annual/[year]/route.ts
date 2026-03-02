import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(_: Request, context: { params: { year: string } }) {
  return proxyBudgetApi(`/api/budget/annual/${context.params.year}`, { method: "GET" });
}

export async function PUT(request: Request, context: { params: { year: string } }) {
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/annual/${context.params.year}`, {
    method: "PUT",
    body
  });
}
