import { useCallback, useState } from "react";
import type { MonthlyWorkspaceResponse } from "@/lib/budget-types";
import type { ActionStatus } from "@/lib/planner-custom-items";
import {
  generateMonthlyActions,
  getMonthlyWorkspace,
  updateMonthlyAction
} from "@/lib/api-clients/monthly-api";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback, type UserFeedback } from "@/lib/http/user-feedback";

type SaveActionItem = {
  actionId: string;
  categoryName: string;
  actualAmount: number | null;
  status: ActionStatus;
};

type SaveActionResult = {
  ok: boolean;
  categoryName: string;
};

async function runWithConcurrency<TItem, TResult>(
  items: TItem[],
  concurrency: number,
  task: (item: TItem) => Promise<TResult>
): Promise<TResult[]> {
  const results: TResult[] = new Array(items.length);
  let index = 0;

  async function worker() {
    while (index < items.length) {
      const currentIndex = index;
      index += 1;
      results[currentIndex] = await task(items[currentIndex]);
    }
  }

  const workerCount = Math.max(1, Math.min(concurrency, items.length));
  await Promise.all(Array.from({ length: workerCount }, () => worker()));
  return results;
}

export function useMonthlyWorkspaceState(initialYear: number, initialMonth: number) {
  const [year, setYear] = useState<number>(initialYear);
  const [month, setMonth] = useState<number>(initialMonth);
  const [workspace, setWorkspace] = useState<MonthlyWorkspaceResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<UserFeedback | null>(null);

  const loadWorkspace = useCallback(async (selectedYear: number, selectedMonth: number) => {
    setLoading(true);
    setMessage(null);

    try {
      try {
        await generateMonthlyActions(selectedYear, selectedMonth);
      } catch (error) {
        if (redirectToLoginIfUnauthorized(error)) {
          return false;
        }

        setMessage(toUserFeedback(error, "Unable to sync annual plan. Showing latest monthly data."));
      }

      const payload = await getMonthlyWorkspace(selectedYear, selectedMonth);
      setWorkspace(payload);
      return true;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return false;
      }

      setWorkspace(null);
      setMessage(toUserFeedback(error, "Unable to load monthly planning data."));
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const saveAllActions = useCallback(
    async (actions: SaveActionItem[]) => {
      setSaving(true);
      setMessage(null);

      let unauthorizedTriggered = false;

      try {
        const results = await runWithConcurrency(actions, 4, async (action): Promise<SaveActionResult> => {
          try {
            await updateMonthlyAction(year, month, action.actionId, {
              status: action.status,
              actualAmount: action.actualAmount
            });

            return {
              ok: true,
              categoryName: action.categoryName
            };
          } catch (error) {
            if (!unauthorizedTriggered && redirectToLoginIfUnauthorized(error)) {
              unauthorizedTriggered = true;
            }

            return {
              ok: false,
              categoryName: action.categoryName
            };
          }
        });

        if (unauthorizedTriggered) {
          return false;
        }

        const successful = results.filter((item) => item.ok).length;
        const failed = results.filter((item) => !item.ok);

        if (failed.length === 0) {
          setMessage({ message: `Saved ${successful} monthly items.` });
        } else {
          const failedPreview = failed
            .slice(0, 3)
            .map((item) => item.categoryName)
            .join(", ");
          const hasMore = failed.length > 3 ? "..." : "";

          setMessage({
            message: `Saved ${successful}/${actions.length} monthly items.`,
            details: `Failed: ${failedPreview}${hasMore}`
          });
        }

        await loadWorkspace(year, month);
        return failed.length === 0;
      } catch (error) {
        if (redirectToLoginIfUnauthorized(error)) {
          return false;
        }

        setMessage(toUserFeedback(error, "Unable to save monthly items."));
        return false;
      } finally {
        setSaving(false);
      }
    },
    [loadWorkspace, month, year]
  );

  return {
    year,
    setYear,
    month,
    setMonth,
    workspace,
    setWorkspace,
    loading,
    saving,
    message,
    setMessage,
    loadWorkspace,
    saveAllActions
  };
}
