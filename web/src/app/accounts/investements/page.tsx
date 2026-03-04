import { AccountsView } from "@/components/budget/accounts-view";
import { BudgetShell } from "@/components/budget/budget-shell";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function AccountsInvestementsPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <AccountsView subpage="investements" />
    </BudgetShell>
  );
}
