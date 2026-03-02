import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST(_: Request, context: { params: { year: string; month: string } }) {
  return proxyBudgetApi(`/api/budget/months/${context.params.year}/${context.params.month}/generate`, {
    method: "POST"
  });
}
