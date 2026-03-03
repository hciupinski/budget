import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET() {
  return proxyBudgetApi("/api/budget/settings/planner-customization", {
    method: "GET"
  });
}

export async function PUT(request: Request) {
  const body = await request.text();

  return proxyBudgetApi("/api/budget/settings/planner-customization", {
    method: "PUT",
    body
  });
}
