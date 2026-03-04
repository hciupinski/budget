import { AccountsView } from "@/components/budget/accounts-view";
import { BudgetShell } from "@/components/budget/budget-shell";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function AccountsSavingsPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <AccountsView subpage="savings" />
    </BudgetShell>
  );
}
