import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PATCH(request: Request, context: { params: Promise<{ goalId: string }> }) {
  const { goalId } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/assets/savings-goals/${goalId}`, {
    method: "PATCH",
    body
  });
}
