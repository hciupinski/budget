import { proxyBudgetApi } from "@/lib/api-proxy";

export async function PATCH(request: Request, context: { params: Promise<{ holdingId: string }> }) {
  const { holdingId } = await context.params;
  const body = await request.text();

  return proxyBudgetApi(`/api/budget/assets/holdings/${holdingId}`, {
    method: "PATCH",
    body
  });
}

export async function DELETE(_: Request, context: { params: Promise<{ holdingId: string }> }) {
  const { holdingId } = await context.params;

  return proxyBudgetApi(`/api/budget/assets/holdings/${holdingId}`, {
    method: "DELETE"
  });
}
