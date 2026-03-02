import { BudgetOverview } from "@/components/budget/budget-overview";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function HomePage() {
  await requireOwnerSession();
  return <BudgetOverview />;
}
