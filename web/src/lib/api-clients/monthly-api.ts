import { requestJson, requestVoid } from "@/lib/http/api-client";
import type { MonthlyWorkspaceResponse } from "@/lib/budget-types";
import type { ActionStatus } from "@/lib/planner-custom-items";

export type UpdateMonthlyActionPayload = {
  status: ActionStatus;
  actualAmount: number | null;
};

export function generateMonthlyActions(year: number, month: number): Promise<void> {
  return requestVoid(`/api/budget/months/${year}/${month}/generate`, {
    method: "POST"
  });
}

export function getMonthlyWorkspace(year: number, month: number): Promise<MonthlyWorkspaceResponse> {
  return requestJson<MonthlyWorkspaceResponse>(`/api/budget/months/${year}/${month}?status=ALL`, {
    method: "GET"
  });
}

export function updateMonthlyAction(
  year: number,
  month: number,
  actionId: string,
  payload: UpdateMonthlyActionPayload
): Promise<void> {
  return requestVoid(`/api/budget/months/${year}/${month}/actions/${actionId}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}
