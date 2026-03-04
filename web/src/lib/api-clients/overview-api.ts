import { requestJson } from "@/lib/http/api-client";
import type { AssetsOverviewResponse } from "@/lib/budget-types";

export function getAssetsOverview(year: number, month: number): Promise<AssetsOverviewResponse> {
  return requestJson<AssetsOverviewResponse>(`/api/budget/assets/overview?year=${year}&month=${month}`, {
    method: "GET"
  });
}
