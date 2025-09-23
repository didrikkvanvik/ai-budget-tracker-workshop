# Workshop Step 004: React Transactions List

## Mission 🎯

In this final step, you'll create a React component that displays the imported transactions in a clean, responsive interface. You'll build the complete data flow from API to UI with proper TypeScript types and error handling.

**Your goal**: Create a functional React component that fetches and displays transaction data with proper formatting, loading states, and responsive design.

**Learning Objectives**:
- React component development with TypeScript
- React Router v7 data loading with useLoaderData
- Shared component usage from template
- Responsive UI design with Tailwind CSS
- Loading states with navigation tracking

---

## Prerequisites

Before starting, ensure you completed:
- [001-transactions.md](001-transactions.md) - Transaction model and database setup
- [002-api.md](002-api.md) - API endpoints
- [003-csv-import.md](003-csv-import.md) - CSV import functionality with test data

---

## Branches

**Starting branch:** `003-csv-import`
**Solution branch:** `004-react-transactions-list`

---

## Step 4.1: Verify TypeScript Types

*The workshop template already includes the TypeScript types for transactions. Let's verify they match our backend.*

Check that `src/BudgetTracker.Web/src/features/transactions/types.ts` exists with the correct interfaces:

```typescript
export interface Transaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  balance?: number;
  category?: string;
  labels?: string;
  importedAt: string;
  sourceFile?: string;
  account: string;
}

export interface TransactionListDto {
  items: Transaction[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
```

*The template includes these types and more - they're already configured to match your backend API.*

## Step 4.2: Verify Transaction API Service

*The workshop template already includes the transaction API service with proper error handling and XSRF token management.*

Verify that `src/BudgetTracker.Web/src/features/transactions/api.ts` exists and contains:

```typescript
import { apiClient } from '../../api';
import type {
  TransactionListDto,
  GetTransactionsParams,
  ImportTransactionsParams,
  ImportResult
} from './types';

export const transactionsApi = {
  async getTransactions(params: GetTransactionsParams = {}): Promise<TransactionListDto> {
    const { page = 1, pageSize = 20 } = params;
    const response = await apiClient.get<TransactionListDto>('/transactions', {
      params: { page, pageSize }
    });
    return response.data;
  },

  async importTransactions(params: ImportTransactionsParams): Promise<ImportResult> {
    const response = await apiClient.post<ImportResult>('/transactions/import', params.formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: params.onUploadProgress
    });
    return response.data;
  }
};
```

*The template provides complete API integration with proper TypeScript types and error handling.*

## Step 4.3: Create Transaction List Component

*This component uses React Router's `useLoaderData` pattern and shared components from the template for a modern, efficient data loading experience.*

Create `src/BudgetTracker.Web/src/features/transactions/components/TransactionList.tsx`:

```tsx
import { useLoaderData, useNavigation } from 'react-router';
import type { TransactionListDto } from '../types';
import EmptyState from '../../../shared/components/EmptyState';
import Pagination from '../../../shared/components/Pagination';
import { SkeletonCardRow } from '../../../shared/components/Skeleton';
import { formatDate } from '../../../shared/utils/formatters';

export default function TransactionList() {
  const data = useLoaderData() as TransactionListDto;
  const navigation = useNavigation();
  const isLoading = navigation.state === 'loading';

  const formatAmount = (amount: number) => {
    const isPositive = amount >= 0;
    const colorClass = isPositive ? 'text-green-600' : 'text-red-600';
    const sign = isPositive ? '+' : '';
    return (
      <span className={`inline-flex items-center font-medium ${colorClass}`}>
        {sign}${Math.abs(amount).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </span>
    );
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 8 }).map((_, index) => (
          <SkeletonCardRow key={index} />
        ))}
      </div>
    );
  }

  if (!data.items || data.items.length === 0) {
    return (
      <EmptyState
        title="No transactions found"
        description="You haven't imported any transactions yet. Start by uploading a CSV file."
        action={{
          label: 'Import Transactions',
          onClick: () => {
            window.location.href = '/import';
          }
        }}
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        {data.items.map((transaction) => (
          <div key={transaction.id} className="bg-white rounded-lg border border-neutral-100 p-4 hover:shadow-sm transition-shadow duration-200">
            <div className="flex justify-between items-start gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-gray-900 truncate">
                    {transaction.description}
                  </p>
                  {transaction.category && (
                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${transaction.category === 'Uncategorized'
                      ? 'bg-red-100 text-red-700'
                      : 'bg-neutral-100 text-neutral-600'
                      }`}>
                      {transaction.category}
                    </span>
                  )}
                  {transaction.account && (
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700">
                      {transaction.account}
                    </span>
                  )}
                </div>
                <p className="text-xs text-gray-500 mt-1">
                  {formatDate(transaction.date)}
                </p>
              </div>
              <div className="flex-shrink-0 text-sm">
                {formatAmount(transaction.amount)}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Pagination */}
      {data.totalPages > 1 && (
        <Pagination
          currentPage={data.page}
          totalPages={data.totalPages}
          totalCount={data.totalCount}
          pageSize={data.pageSize}
          className="mt-6"
        />
      )}
    </div>
  );
}
```

**Key Differences from Traditional React Patterns:**

- **`useLoaderData()`**: Data is loaded by React Router before the component renders, eliminating loading states
- **`useNavigation()`**: Tracks navigation state for loading indicators during page transitions
- **Shared Components**: Uses template's `EmptyState`, `Pagination`, and `SkeletonCardRow` components
- **Utility Functions**: Leverages `formatDate` from shared formatters
- **No useState/useEffect**: React Router handles all data fetching and state management

## Step 4.4: Add Data Loader for React Router

*React Router v7 uses data loaders to fetch data before components render. This creates a better user experience with instant page loads.*

Update your route configuration to include the transaction data loader. In your router setup file, add:

```tsx
import { transactionsApi } from '../features/transactions/api';

// Data loader for transactions page
export async function transactionsLoader({ request }: LoaderFunctionArgs) {
  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get('page') || '1', 10);
  const pageSize = parseInt(url.searchParams.get('pageSize') || '20', 10);

  return await transactionsApi.getTransactions({ page, pageSize });
}

// Add to your route configuration:
{
  path: '/transactions',
  element: <TransactionsPage />,
  loader: transactionsLoader
}
```

*This loader runs before the component renders, providing instant data access with `useLoaderData()`.*

## Step 4.5: Verify Transactions Page Route

*The workshop template already includes routing configuration with React Router v7. Verify the transactions route is properly configured.*

Check that your routing includes the transactions page with the data loader:

```tsx
// In your router configuration (typically router.tsx or main routing file)
{
  path: '/transactions',
  element: <TransactionList />,
  loader: transactionsLoader
}
```

*The template provides complete routing setup with proper data loaders and authentication guards.*

## Step 4.6: Verify Navigation

*The workshop template includes a complete navigation system. Verify that the transactions link is available in the navigation.*

Check that your navigation component includes links to the transactions page:

```tsx
// In your Layout or Navigation component
<Link to="/transactions" className="nav-link">
  Transactions
</Link>
```

*The template provides complete navigation with proper styling and active states.*

## Step 4.7: Verify Dashboard Links

*The workshop template includes a complete dashboard. Verify it includes links to view transactions.*

Check that your dashboard includes navigation to the transactions page:

```tsx
// In your Dashboard component
<Link to="/transactions" className="dashboard-card-link">
  View Transactions
</Link>
```

*The template provides complete dashboard functionality with proper navigation between features.*

## Step 4.8: Test the Transaction List Component

1. **Start the Backend**:
   ```bash
   cd src/BudgetTracker.Api/
   dotnet run
   ```

2. **Start the Frontend**:
   ```bash
   cd src/BudgetTracker.Web/
   npm run dev
   ```

3. **Test the Component**:
   - Visit `http://localhost:5173`
   - Login/Register (use existing auth system)
   - Navigate to Transactions page
   - Verify the component loads data using React Router's loader
   - Test pagination if you have multiple pages of data
   - Verify skeleton loading states during navigation

If you haven't imported any transactions yet, you should see the EmptyState component. You can import transactions using the API endpoint from step 3 with one of the sample CSV files from `samples/`.

---

## Expected UI Behavior

### Empty State
- Shows "No transactions" message
- Displays helpful text about importing transactions
- Clean, centered layout

### With Data
- Lists transactions with proper formatting
- Positive amounts in green, negative in red
- Category and account badges
- Responsive design that works on mobile

### Loading State
- Skeleton placeholders while loading
- Smooth transitions

### Error State
- Clear error messages
- Retry button functionality

---

## Troubleshooting 🔧

### Common Issues

1. **CORS Errors**:
   - Ensure API is running on port 5295
   - Check CORS configuration in `Program.cs`

2. **Empty Transaction List**:
   - Verify you imported transactions in step 3 using one of the sample CSV files from `samples/`
   - Check API key is correct (`test-key-user1`)
   - Use browser dev tools to inspect API calls

3. **TypeScript Errors**:
   - Ensure all type definitions are correct
   - Check import paths

4. **Styling Issues**:
   - Verify Tailwind CSS is configured properly
   - Check that all required classes are available

---

## Summary ✅

You've successfully created:

✅ **TypeScript Types**: Type-safe interfaces for API communication
✅ **API Service**: Axios-based service for transaction data fetching
✅ **Transaction List Component**: Complete React component with loading, error, and empty states
✅ **Page Component**: Properly structured page with layout
✅ **Navigation**: Updated routing and navigation links
✅ **Responsive Design**: Mobile-friendly UI with Tailwind CSS

**Complete Workshop Achievement**: You now have a fully functional CSV import and transaction display system!

**Key Learning Points**:
- React Router v7 data loading patterns
- Component composition with shared components
- TypeScript integration with proper typing
- Modern React patterns without useState/useEffect
- Full-stack data flow with efficient loading

Your budget tracker now has a complete feature for importing CSV files and displaying transactions. Great work! 🎉