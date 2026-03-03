import { proxyBudgetApi } from "@/lib/api-proxy";

export async function POST(
  _: Request,
  context: { params: Promise<{ year: string; sourceYear: string }> }
) {
  const { year, sourceYear } = await context.params;

  return proxyBudgetApi(`/api/budget/annual/${year}/copy-from/${sourceYear}`, {
    method: "POST"
  });
}
