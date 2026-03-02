"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  MONTH_LABELS,
  MONTHLY_STATUSES,
  type AuditEntryResponse,
  type MonthlyStatus,
  type MonthlyWorkspaceResponse
} from "@/lib/budget-types";

type ActionStatus = Exclude<MonthlyStatus, "ALL">;

function asCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2
  }).format(value);
}

export function MonthlyWorkspace() {
  const now = new Date();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [month, setMonth] = useState<number>(now.getMonth() + 1);
  const [filter, setFilter] = useState<MonthlyStatus>("ALL");
  const [workspace, setWorkspace] = useState<MonthlyWorkspaceResponse | null>(null);
  const [auditEntries, setAuditEntries] = useState<AuditEntryResponse[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [message, setMessage] = useState<string | null>(null);
  const [savingActionId, setSavingActionId] = useState<string | null>(null);

  function redirectToLoginIfUnauthorized(statusCode: number): boolean {
    if (statusCode === 401) {
      window.location.assign("/login");
      return true;
    }

    return false;
  }

  const loadWorkspace = useCallback(async () => {
    setLoading(true);

    const response = await fetch(`/api/budget/months/${year}/${month}?status=${filter}`, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setWorkspace(null);
      setMessage("Unable to load monthly workspace.");
      setLoading(false);
      return;
    }

    const data = (await response.json()) as MonthlyWorkspaceResponse;
    setWorkspace(data);
    setLoading(false);
  }, [filter, month, year]);

  const loadAudit = useCallback(async () => {
    const response = await fetch(`/api/budget/audit?year=${year}&month=${month}&limit=20`, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setAuditEntries([]);
      return;
    }

    setAuditEntries((await response.json()) as AuditEntryResponse[]);
  }, [month, year]);

  useEffect(() => {
    setMessage(null);
    void Promise.all([loadWorkspace(), loadAudit()]);
  }, [filter, loadAudit, loadWorkspace, month, year]);

  async function generateFromAnnualPlan() {
    const response = await fetch(`/api/budget/months/${year}/${month}/generate`, {
      method: "POST"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setMessage("Failed to generate monthly actions.");
      return;
    }

    setMessage("Monthly actions generated from annual plan.");
    await Promise.all([loadWorkspace(), loadAudit()]);
  }

  function updateDraft(actionId: string, change: Partial<MonthlyWorkspaceResponse["actions"][number]>) {
    if (!workspace) {
      return;
    }

    setWorkspace({
      ...workspace,
      actions: workspace.actions.map((action) =>
        action.actionId === actionId
          ? {
              ...action,
              ...change
            }
          : action
      )
    });
  }

  async function saveAction(actionId: string) {
    if (!workspace) {
      return;
    }

    const action = workspace.actions.find((current) => current.actionId === actionId);
    if (!action) {
      return;
    }

    setSavingActionId(actionId);

    const response = await fetch(`/api/budget/months/${year}/${month}/actions/${actionId}`, {
      method: "PATCH",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        status: action.status,
        actualAmount: action.actualAmount
      })
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setSavingActionId(null);
      setMessage(`Failed to save ${action.categoryName}.`);
      return;
    }

    setSavingActionId(null);
    setMessage(`Updated ${action.categoryName}.`);
    await Promise.all([loadWorkspace(), loadAudit()]);
  }

  const actions = useMemo(() => workspace?.actions ?? [], [workspace]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Monthly Workspace</CardTitle>
        <CardDescription>Execute the month, track statuses, and update actual amounts.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="number"
            min={2000}
            max={2100}
            className="w-28"
            value={year}
            onChange={(event) => setYear(Number.parseInt(event.target.value, 10) || year)}
          />
          <select
            className="h-10 rounded-md border px-3 text-sm"
            value={month}
            onChange={(event) => setMonth(Number.parseInt(event.target.value, 10))}
          >
            {MONTH_LABELS.map((label, index) => (
              <option key={label} value={index + 1}>
                {label}
              </option>
            ))}
          </select>
          <select
            className="h-10 rounded-md border px-3 text-sm"
            value={filter}
            onChange={(event) => setFilter(event.target.value as MonthlyStatus)}
          >
            {MONTHLY_STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
          <Button type="button" variant="secondary" onClick={generateFromAnnualPlan}>
            Generate from Annual Plan
          </Button>
          <Button type="button" variant="outline" onClick={() => void Promise.all([loadWorkspace(), loadAudit()])}>
            Refresh
          </Button>
          {message ? <p className="text-sm text-muted-foreground">{message}</p> : null}
        </div>

        {workspace ? (
          <div className="grid gap-3 md:grid-cols-4">
            <SummaryCard
              title="Income"
              planned={workspace.summary.incomePlanned}
              actual={workspace.summary.incomeActual}
            />
            <SummaryCard title="Costs" planned={workspace.summary.costsPlanned} actual={workspace.summary.costsActual} />
            <SummaryCard
              title="Savings / Invest"
              planned={workspace.summary.savingsPlanned}
              actual={workspace.summary.savingsActual}
            />
            <SummaryCard
              title="Remainder"
              planned={workspace.summary.remainderPlanned}
              actual={workspace.summary.remainderActual}
            />
          </div>
        ) : null}

        {workspace ? (
          <div className="rounded-md border bg-background p-3 text-sm">
            Completion: {workspace.completion.done} done, {workspace.completion.partial} partial, {workspace.completion.skipped} skipped, {workspace.completion.planned} planned ({workspace.completion.total} total)
          </div>
        ) : null}

        {loading ? <p className="text-sm text-muted-foreground">Loading monthly workspace...</p> : null}

        {!loading ? (
          <div className="overflow-x-auto pb-1">
            <table className="w-max min-w-full border-collapse text-sm">
              <thead>
                <tr>
                  <th className="min-w-[170px] border px-2 py-2 text-left">Category</th>
                  <th className="min-w-[180px] border px-2 py-2 text-left">Section</th>
                  <th className="min-w-[140px] border px-2 py-2 text-right whitespace-nowrap">Planned</th>
                  <th className="w-[120px] min-w-[120px] border px-2 py-2 text-right whitespace-nowrap">Actual</th>
                  <th className="min-w-[130px] border px-2 py-2 text-left">Status</th>
                  <th className="min-w-[190px] border px-2 py-2 text-left">Updated</th>
                  <th className="min-w-[95px] border px-2 py-2 text-left">Action</th>
                </tr>
              </thead>
              <tbody>
                {actions.map((action) => (
                  <tr key={action.actionId}>
                    <td className="border px-2 py-2 whitespace-nowrap">{action.categoryName}</td>
                    <td className="border px-2 py-2 whitespace-nowrap">{action.section}</td>
                    <td className="border px-2 py-2 text-right tabular-nums whitespace-nowrap">
                      {asCurrency(action.plannedAmount)}
                    </td>
                    <td className="w-[120px] min-w-[120px] border px-2 py-2">
                      <Input
                        type="number"
                        step="0.01"
                        className="numeric-input h-8 min-w-[112px] px-2 text-right text-sm tabular-nums"
                        value={action.actualAmount ?? ""}
                        onChange={(event) => {
                          const parsed = Number.parseFloat(event.target.value);
                          updateDraft(action.actionId, {
                            actualAmount: Number.isFinite(parsed) ? parsed : null
                          });
                        }}
                      />
                    </td>
                    <td className="border px-2 py-2">
                      <select
                        className="h-8 rounded-md border px-2 text-sm"
                        value={action.status}
                        onChange={(event) => {
                          updateDraft(action.actionId, {
                            status: event.target.value as ActionStatus
                          });
                        }}
                      >
                        {MONTHLY_STATUSES.filter((status) => status !== "ALL").map((status) => (
                          <option key={`${action.actionId}-${status}`} value={status}>
                            {status}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="border px-2 py-2 text-xs text-muted-foreground whitespace-nowrap">
                      {new Date(action.updatedAt).toLocaleString()}
                    </td>
                    <td className="border px-2 py-2">
                      <Button
                        type="button"
                        size="sm"
                        onClick={() => void saveAction(action.actionId)}
                        disabled={savingActionId === action.actionId}
                      >
                        {savingActionId === action.actionId ? "Saving..." : "Save"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}

        <div className="space-y-2 rounded-md border bg-background p-3">
          <h3 className="text-sm font-semibold">Recent Audit Trail</h3>
          {auditEntries.length === 0 ? <p className="text-sm text-muted-foreground">No recent changes.</p> : null}
          {auditEntries.map((entry) => (
            <div key={entry.id} className="rounded border p-2 text-xs">
              <p className="font-medium">
                {entry.eventType} on {entry.entityType}
              </p>
              <p className="text-muted-foreground">
                {new Date(entry.changedAt).toLocaleString()} by {entry.changedBy}
              </p>
              <pre className="mt-1 overflow-x-auto whitespace-pre-wrap text-[11px]">{entry.payload}</pre>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function SummaryCard({ title, planned, actual }: { title: string; planned: number; actual: number }) {
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{title}</p>
      <p className="text-sm">Planned: {asCurrency(planned)}</p>
      <p className="text-sm font-semibold">Actual: {asCurrency(actual)}</p>
    </div>
  );
}
