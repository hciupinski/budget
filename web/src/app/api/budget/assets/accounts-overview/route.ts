import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const year = url.searchParams.get("year");
  const month = url.searchParams.get("month");
  const query = new URLSearchParams();

  if (year) {
    query.set("year", year);
  }

  if (month) {
    query.set("month", month);
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : "";

  return proxyBudgetApi(`/api/budget/assets/accounts-overview${suffix}`, {
    method: "GET"
  });
}
