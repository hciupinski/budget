// TODO: Replace this placeholder dataset with real API integration.
export type TransactionType = "INCOME" | "EXPENSE";

export type TransactionRecord = {
  id: string;
  date: string;
  description: string;
  category: string;
  type: TransactionType;
  status: "paid";
  amount: number;
};

export const TRANSACTIONS: TransactionRecord[] = [
  {
    id: "1",
    date: "Dec 1, 2024",
    description: "Client A - Consulting",
    category: "Consulting",
    type: "INCOME",
    status: "paid",
    amount: 5000
  },
  {
    id: "2",
    date: "Dec 15, 2024",
    description: "Client B - Development",
    category: "Development",
    type: "INCOME",
    status: "paid",
    amount: 3500
  },
  {
    id: "3",
    date: "Dec 5, 2024",
    description: "Adobe Creative Suite",
    category: "Software & Tools",
    type: "EXPENSE",
    status: "paid",
    amount: -550
  },
  {
    id: "4",
    date: "Dec 8, 2024",
    description: "GitHub Pro",
    category: "Software & Tools",
    type: "EXPENSE",
    status: "paid",
    amount: -300
  },
  {
    id: "5",
    date: "Dec 10, 2024",
    description: "Office Desk",
    category: "Office Supplies",
    type: "EXPENSE",
    status: "paid",
    amount: -1200
  },
  {
    id: "6",
    date: "Dec 12, 2024",
    description: "Accountant Fee",
    category: "Professional Services",
    type: "EXPENSE",
    status: "paid",
    amount: -450
  }
];
