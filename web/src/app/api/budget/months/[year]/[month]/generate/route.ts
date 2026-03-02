import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST(
  _: Request,
  context: { params: Promise<{ year: string; month: string }> }
) {
  const { year, month } = await context.params;

  return proxyBudgetApi(`/api/budget/months/${year}/${month}/generate`, {
    method: "POST"
  });
}
