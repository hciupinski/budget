import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PUT(
  request: Request,
  context: { params: Promise<{ year: string; month: string }> }
) {
  const { year, month } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/assets/snapshots/${year}/${month}`, {
    method: "PUT",
    body
  });
}
