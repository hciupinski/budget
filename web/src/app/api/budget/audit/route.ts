import { proxyBudgetApi } from "@/lib/api-proxy";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const params = new URLSearchParams();

  const year = url.searchParams.get("year");
  const month = url.searchParams.get("month");
  const limit = url.searchParams.get("limit");

  if (year) {
    params.set("year", year);
  }

  if (month) {
    params.set("month", month);
  }

  if (limit) {
    params.set("limit", limit);
  }

  const suffix = params.toString();

  return proxyBudgetApi(`/api/budget/audit${suffix ? `?${suffix}` : ""}`, {
    method: "GET"
  });
}
