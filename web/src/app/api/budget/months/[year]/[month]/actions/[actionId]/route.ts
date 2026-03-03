import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PATCH(
  request: Request,
  context: { params: Promise<{ year: string; month: string; actionId: string }> }
) {
  const { year, month, actionId } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(
    `/api/budget/months/${year}/${month}/actions/${actionId}`,
    {
      method: "PATCH",
      body
    }
  );
}
