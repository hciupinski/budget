import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PATCH(request: Request, context: { params: Promise<{ accountId: string }> }) {
  const { accountId } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/assets/accounts/${accountId}`, {
    method: "PATCH",
    body
  });
}
