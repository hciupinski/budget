import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PATCH(request: Request, context: { params: { year: string; month: string; actionId: string } }) {
  const body = await request.text();

  return proxyBudgetApi(
    `/api/budget/months/${context.params.year}/${context.params.month}/actions/${context.params.actionId}`,
    {
      method: "PATCH",
      body
    }
  );
}
