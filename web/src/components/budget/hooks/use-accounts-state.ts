import { useCallback, useState } from "react";
import type { AssetsAccountsOverviewResponse, AssetsInvestmentsResponse } from "@/lib/budget-types";
import { getAccountsOverview, getInvestments } from "@/lib/api-clients/accounts-api";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback, type UserFeedback } from "@/lib/http/user-feedback";

export type AccountsSubpage = "general" | "savings" | "investments";

export function useAccountsState(initialYear: number, initialMonth: number, subpage: AccountsSubpage) {
  const [year, setYear] = useState<number>(initialYear);
  const [month, setMonth] = useState<number>(initialMonth);
  const [accountsOverview, setAccountsOverview] = useState<AssetsAccountsOverviewResponse | null>(null);
  const [investmentsData, setInvestmentsData] = useState<AssetsInvestmentsResponse | null>(null);
  const [loadingAccounts, setLoadingAccounts] = useState<boolean>(true);
  const [loadingInvestments, setLoadingInvestments] = useState<boolean>(false);
  const [message, setMessage] = useState<UserFeedback | null>(null);

  const loadAccountsOverview = useCallback(async (selectedYear: number, selectedMonth: number) => {
    setLoadingAccounts(true);

    try {
      const payload = await getAccountsOverview(selectedYear, selectedMonth);
      setAccountsOverview(payload);
      setMessage(null);
      return payload;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return null;
      }

      setMessage(toUserFeedback(error, "Unable to load accounts data."));
      setAccountsOverview(null);
      return null;
    } finally {
      setLoadingAccounts(false);
    }
  }, []);

  const loadInvestments = useCallback(async () => {
    setLoadingInvestments(true);

    try {
      const payload = await getInvestments("auto");
      setInvestmentsData(payload);
      setMessage(null);
      return payload;
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return null;
      }

      setMessage(toUserFeedback(error, "Unable to load investments data."));
      setInvestmentsData(null);
      return null;
    } finally {
      setLoadingInvestments(false);
    }
  }, []);

  const refreshData = useCallback(async () => {
    await loadAccountsOverview(year, month);
    if (subpage === "investments") {
      await loadInvestments();
    }
  }, [loadAccountsOverview, loadInvestments, month, subpage, year]);

  return {
    year,
    setYear,
    month,
    setMonth,
    accountsOverview,
    setAccountsOverview,
    investmentsData,
    setInvestmentsData,
    loadingAccounts,
    loadingInvestments,
    message,
    setMessage,
    loadAccountsOverview,
    loadInvestments,
    refreshData
  };
}
