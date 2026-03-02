import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST(_: Request, context: { params: { year: string; sourceYear: string } }) {
  return proxyBudgetApi(`/api/budget/annual/${context.params.year}/copy-from/${context.params.sourceYear}`, {
    method: "POST"
  });
}
