import { requestJson } from "@/lib/http/api-client";
import type { AnnualPlanResponse } from "@/lib/budget-types";

export type SaveAnnualPlanPayload = {
  cells: Array<{
    categoryId: string;
    month: number;
    plannedAmount: number;
  }>;
};

export function getAnnualPlan(year: number): Promise<AnnualPlanResponse> {
  return requestJson<AnnualPlanResponse>(`/api/budget/annual/${year}`, {
    method: "GET"
  });
}

export function saveAnnualPlan(year: number, payload: SaveAnnualPlanPayload): Promise<AnnualPlanResponse> {
  return requestJson<AnnualPlanResponse>(`/api/budget/annual/${year}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function copyAnnualPlanFromYear(year: number, sourceYear: number): Promise<AnnualPlanResponse> {
  return requestJson<AnnualPlanResponse>(`/api/budget/annual/${year}/copy-from/${sourceYear}`, {
    method: "POST"
  });
}
