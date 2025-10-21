import { format } from 'date-fns';

/**
 * Format currency amounts with proper sign and locale formatting
 */
export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(amount);
}

/**
 * Format amounts with sign prefix and consistent decimal places
 */
export function formatAmount(amount: number): string {
  const sign = amount < 0 ? '-' : '+';
  return `${sign}$${Math.abs(amount).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  })}`;
}

/**
 * Format percentage values
 */
export function formatPercentage(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'percent',
    minimumFractionDigits: 1,
    maximumFractionDigits: 1
  }).format(value / 100);
}

/**
 * Format large numbers with abbreviations (K, M, B)
 */
export function formatCompactNumber(value: number): string {
  return new Intl.NumberFormat('en-US', {
    notation: 'compact',
    compactDisplay: 'short',
    maximumFractionDigits: 1
  }).format(value);
}

/**
 * Format date strings consistently across the app
 */
export function formatDate(dateString: string): string {
  return format(new Date(dateString), 'MMM dd, yyyy');
}

/**
 * Format date with time
 */
export function formatDateTime(dateString: string): string {
  return format(new Date(dateString), 'MMM dd, yyyy • h:mm a');
}

/**
 * Format date for display in forms (YYYY-MM-DD)
 */
export function formatDateForInput(dateString: string): string {
  return format(new Date(dateString), 'yyyy-MM-dd');
}

/**
 * Get consistent color classes for category tags based on category name
 * Same category will always return the same color
 */
export function getCategoryColor(category: string): string {
  // Special case for Uncategorized
  if (category === 'Uncategorized') {
    return 'bg-red-100 text-red-700';
  }

  // Define color palette for categories
  const colors = [
    'bg-blue-100 text-blue-700',
    'bg-green-100 text-green-700',
    'bg-purple-100 text-purple-700',
    'bg-yellow-100 text-yellow-700',
    'bg-pink-100 text-pink-700',
    'bg-indigo-100 text-indigo-700',
    'bg-orange-100 text-orange-700',
    'bg-teal-100 text-teal-700',
    'bg-cyan-100 text-cyan-700',
    'bg-lime-100 text-lime-700',
    'bg-emerald-100 text-emerald-700',
    'bg-violet-100 text-violet-700',
    'bg-fuchsia-100 text-fuchsia-700',
    'bg-rose-100 text-rose-700',
    'bg-sky-100 text-sky-700',
    'bg-amber-100 text-amber-700',
  ];

  // Generate consistent hash from category name
  let hash = 0;
  for (let i = 0; i < category.length; i++) {
    hash = category.charCodeAt(i) + ((hash << 5) - hash);
  }

  // Use absolute value to ensure positive index
  const index = Math.abs(hash) % colors.length;
  return colors[index];
}