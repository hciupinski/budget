import { BudgetShell } from "@/components/budget/budget-shell";
import { TransactionsView } from "@/components/budget/transactions-view";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function TransactionsPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <TransactionsView />
    </BudgetShell>
  );
}
