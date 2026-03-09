import { useCallback, useState } from "react";
import type { AnnualPlanResponse } from "@/lib/budget-types";
import {
  copyAnnualPlanFromYear,
  getAnnualPlan,
  saveAnnualPlan,
  type SaveAnnualPlanPayload
} from "@/lib/api-clients/annual-api";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback, type UserFeedback } from "@/lib/http/user-feedback";

function mapPlanToSavePayload(plan: AnnualPlanResponse): SaveAnnualPlanPayload {
  return {
    cells: plan.categories.flatMap((row) =>
      row.months.map((plannedAmount, index) => ({
        categoryId: row.categoryId,
        month: index + 1,
        plannedAmount
      }))
    )
  };
}

export function useAnnualPlannerState(initialYear: number) {
  const [year, setYear] = useState<number>(initialYear);
  const [annualPlan, setAnnualPlan] = useState<AnnualPlanResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<UserFeedback | null>(null);

  const loadAnnualPlanData = useCallback(async (selectedYear: number) => {
    setLoading(true);
    setMessage(null);

    try {
      const payload = await getAnnualPlan(selectedYear);
      setAnnualPlan(payload);
      return true;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return false;
      }

      setAnnualPlan(null);
      setMessage(toUserFeedback(error, "Unable to load annual plan."));
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const saveAnnualPlanData = useCallback(async (plan: AnnualPlanResponse, selectedYear: number) => {
    setSaving(true);
    setMessage(null);

    try {
      const payload = mapPlanToSavePayload(plan);
      const next = await saveAnnualPlan(selectedYear, payload);
      setAnnualPlan(next);
      setMessage({ message: "Annual plan saved." });
      return true;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return false;
      }

      setMessage(toUserFeedback(error, "Save failed."));
      return false;
    } finally {
      setSaving(false);
    }
  }, []);

  const copyFromPreviousYear = useCallback(async (selectedYear: number) => {
    setSaving(true);
    setMessage(null);

    try {
      const copied = await copyAnnualPlanFromYear(selectedYear, selectedYear - 1);
      setAnnualPlan(copied);
      setMessage({ message: `Copied annual plan from ${selectedYear - 1}.` });
      return true;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return false;
      }

      setMessage(toUserFeedback(error, `Cannot copy from ${selectedYear - 1}.`));
      return false;
    } finally {
      setSaving(false);
    }
  }, []);

  return {
    year,
    setYear,
    annualPlan,
    setAnnualPlan,
    loading,
    saving,
    message,
    setMessage,
    loadAnnualPlanData,
    saveAnnualPlanData,
    copyFromPreviousYear
  };
}
