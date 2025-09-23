# Workshop Step 043: Natural Language Query Assistant - Web Integration

## Mission 🎯

In this step, you'll integrate the Natural Language Query Assistant into the React frontend, creating a conversational interface that allows users to ask questions about their finances in plain English and receive intelligent responses with relevant transaction data.

**Your goal**: Build a complete frontend interface for the RAG-powered query assistant, including API integration, responsive UI components, and real-time query processing.

**Learning Objectives**:
- Creating API clients for AI service integration
- Building interactive query interfaces with React
- Implementing real-time loading states and error handling
- Designing conversational UI patterns
- Integrating semantic search results into user interfaces
- Managing complex state for AI-powered features

---

## Prerequisites

Before starting, ensure you completed:
- [042-rag-semantic-search.md](042-rag-semantic-search.md) - Backend semantic search infrastructure
- Basic React development knowledge
- Authentication system from section-05

You should have:
- Working backend API with query endpoints
- React application with authentication
- Tailwind CSS for styling

---

## Branches

**Starting branch:** `043-rag-nlq`  
**Solution branch:** `044-rag-nlq-web`  

---

## Step 43.1: Create LoadingSpinner Component

*Build a reusable loading spinner component for AI processing states.*

AI queries can take several seconds to process, so we need a good loading indicator to provide user feedback during processing.

Create `src/BudgetTracker.Web/src/shared/components/LoadingSpinner.tsx`:

```tsx
interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
}

export function LoadingSpinner({ size = 'md' }: LoadingSpinnerProps) {
  const sizeClasses = {
    sm: 'h-4 w-4',
    md: 'h-8 w-8',
    lg: 'h-12 w-12'
  };

  return (
    <div className="flex items-center justify-center">
      <div className={`animate-spin rounded-full border-b-2 border-indigo-600 ${sizeClasses[size]}`}></div>
    </div>
  );
}
```

## Step 43.2: Create Intelligence API Types

*Define TypeScript interfaces for the query assistant API.*

Create `src/BudgetTracker.Web/src/features/intelligence/api.ts`:

```typescript
import api from '../../api/client';

export interface QueryRequest {
  query: string;
}

export interface TransactionDto {
  id: string;
  date: string;
  description: string;
  amount: number;
  balance?: number;
  category?: string;
  labels?: string;
  importedAt: string;
  account: string;
}

export interface QueryResponse {
  answer: string;
  amount?: number;
  transaction?: TransactionDto;
  transactions?: TransactionDto[];
}

export const intelligenceApi = {
  askQuery: async (query: string): Promise<QueryResponse> => {
    const response = await api.post<QueryResponse>('/query/ask', { query });
    return response.data;
  }
};
```

## Step 43.3: Create Query Assistant Component

*Build the main conversational interface component.*

Create `src/BudgetTracker.Web/src/features/intelligence/components/QueryAssistant.tsx`:

```tsx
import { useState } from 'react';
import { useToast } from '../../../shared/contexts/ToastContext';
import { intelligenceApi, type QueryResponse } from '../api';
import Card from '../../../shared/components/Card';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';
import { formatCurrency, formatDate } from '../../../shared/utils/formatters';

const MessageIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-message-circle">
    <path d="M7.9 20A9 9 0 1 0 4 16.1L2 22Z" />
  </svg>
);

const SendIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-send">
    <path d="m22 2-7 20-4-9-9-4Z" />
    <path d="M22 2 11 13" />
  </svg>
);

interface QueryAssistantProps {
  className?: string;
}

export default function QueryAssistant({ className = "" }: QueryAssistantProps) {
  const [query, setQuery] = useState('');
  const [response, setResponse] = useState<QueryResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { showToast } = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim() || isLoading) return;

    setIsLoading(true);
    try {
      const result = await intelligenceApi.askQuery(query.trim());
      setResponse(result);
    } catch (error) {
      console.error('Query failed:', error);
      showToast('error', 'Failed to process your query. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const suggestedQueries = [
    "What was my biggest expense last week?",
    "How much did I spend on groceries this month?",
    "Show me all transactions over €100",
    "What's my average daily spending?",
    "Which category do I spend the most on?",
    "When did I last go to Starbucks?"
  ];

  return (
    <Card className={`p-6 ${className}`}>
      <div className="flex items-center gap-3 mb-4">
        <div className="p-2 bg-indigo-100 rounded-lg">
          <MessageIcon />
        </div>
        <div>
          <h3 className="font-semibold text-gray-900">Ask about your finances</h3>
          <p className="text-sm text-gray-600">Ask me anything about your spending and transactions</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mb-4">
        <div className="flex gap-2">
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Ask a question about your finances..."
            className="flex-1 px-3 py-2 border border-gray-300 rounded-md text-sm placeholder:text-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            disabled={isLoading}
          />
          <button
            type="submit"
            disabled={!query.trim() || isLoading}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isLoading ? <LoadingSpinner size="sm" /> : <SendIcon />}
            Ask
          </button>
        </div>
      </form>

      {!response && !isLoading && (
        <div className="space-y-2">
          <p className="text-sm text-gray-600 mb-2">Try asking:</p>
          <div className="flex flex-wrap gap-2">
            {suggestedQueries.map((suggestion, index) => (
              <button
                key={index}
                onClick={() => setQuery(suggestion)}
                className="text-xs px-3 py-1 bg-gray-100 text-gray-600 rounded-full hover:bg-gray-200 transition-colors"
              >
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      )}

      {response && (
        <div className="mt-4 p-4 bg-gray-50 rounded-lg">
          <p className="text-sm text-gray-900 mb-3">{response.answer}</p>

          {response.transaction && (
            <div className="mt-3 p-3 bg-white rounded-md border">
              <div className="flex justify-between items-start">
                <div className="flex-1">
                  <p className="font-medium text-sm">{response.transaction.description}</p>
                  <p className="text-xs text-gray-500 mt-1">
                    {formatDate(response.transaction.date)} • {response.transaction.account}
                  </p>
                  {response.transaction.category && (
                    <span className="inline-block mt-1 px-2 py-1 bg-indigo-100 text-indigo-700 text-xs rounded-full">
                      {response.transaction.category}
                    </span>
                  )}
                </div>
                <div className="text-right">
                  <p className={`font-medium text-sm ${response.transaction.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                    {response.transaction.amount < 0 ? '-' : '+'}{formatCurrency(Math.abs(response.transaction.amount))}
                  </p>
                </div>
              </div>
            </div>
          )}

          {response.transactions && response.transactions.length > 0 && (
            <div className="mt-3 space-y-2">
              {response.transactions.slice(0, 3).map((transaction) => (
                <div key={transaction.id} className="p-3 bg-white rounded-md border">
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <p className="font-medium text-sm">{transaction.description}</p>
                      <p className="text-xs text-gray-500 mt-1">
                        {formatDate(transaction.date)} • {transaction.account}
                      </p>
                      {transaction.category && (
                        <span className="inline-block mt-1 px-2 py-1 bg-indigo-100 text-indigo-700 text-xs rounded-full">
                          {transaction.category}
                        </span>
                      )}
                    </div>
                    <div className="text-right">
                      <p className={`font-medium text-sm ${transaction.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                        {transaction.amount < 0 ? '-' : '+'}{formatCurrency(Math.abs(transaction.amount))}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
              {response.transactions.length > 3 && (
                <p className="text-xs text-gray-500 text-center">
                  ... and {response.transactions.length - 3} more transactions
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
```

## Step 43.4: Create Intelligence Module Index

*Create an index file to export intelligence components.*

Create `src/BudgetTracker.Web/src/features/intelligence/index.ts`:

```typescript
export { default as QueryAssistant } from './components/QueryAssistant';
export { intelligenceApi } from './api';
export type { QueryResponse, TransactionDto } from './api';
```

## Step 43.5: Update Dashboard with Query Assistant

*Integrate the Query Assistant into the main dashboard.*

Update `src/BudgetTracker.Web/src/routes/dashboard.tsx`:

```tsx
import { Link } from 'react-router-dom';
import Header from '../shared/components/layout/Header';
import { QueryAssistant } from '../features/intelligence';

export async function loader() {
  return {};
}

export default function Dashboard() {
  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Dashboard"
        subtitle="Welcome to your budget tracker"
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        <QueryAssistant className="lg:col-span-2" />

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Transactions</h3>
          <p className="text-gray-600 mb-4">
            View and manage your imported transaction data.
          </p>
          <Link
            to="/transactions"
            className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-colors duration-200"
          >
            View Transactions
          </Link>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Spending Summary</h3>
          <p className="text-gray-600">
            Spending charts will be implemented in future steps.
          </p>
        </div>
      </div>
    </div>
  );
}
```

## Step 43.6: Add Error Handling for API

*Enhance the API client with better error handling for AI services.*

Update `src/BudgetTracker.Web/src/api/client.ts` to handle AI service errors:

```typescript
import axios from 'axios';
import { navigationService } from '../shared/services/navigationService';

const baseURL = import.meta.env.VITE_API_BASE_URL
  || (import.meta.env.DEV ? 'http://localhost:5295/api' : '/api');

const api = axios.create({
  baseURL,
  timeout: 50000,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true
});

// Add XSRF token for non-GET requests
api.interceptors.request.use(config => {
  if (config.method?.toLowerCase() !== 'get') {
    const token = document.cookie
      .split(';')
      .find(c => c.trim().startsWith('XSRF-TOKEN='))
      ?.split('=')[1];

    if (token) {
      config.headers['RequestVerificationToken'] = token;
    }
  }
  return config;
});

// Handle auth errors and redirects
api.interceptors.response.use(
  response => response,
  error => {
    const isAuthEndpoint = ['/users/login', '/users/register', '/users/me'].some(
      endpoint => error.config?.url?.endsWith(endpoint)
    );

    if (error.response?.status === 401 && !isAuthEndpoint) {
      navigationService.navigateTo('/login');
    }

    // Handle specific AI service errors
    if (error.config?.url?.includes('/query/ask')) {
      console.error('Query Assistant Error:', error.response?.data || error.message);
    }

    return Promise.reject(error);
  }
);

export default api;
```

## Step 43.7: Add Loading State Improvements

*Improve the loading experience with better visual feedback.*

Update the form section in QueryAssistant to show loading state on the input:

```tsx
<form onSubmit={handleSubmit} className="mb-4">
  <div className="flex gap-2">
    <div className="flex-1 relative">
      <input
        type="text"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder={isLoading ? "Processing your question..." : "Ask a question about your finances..."}
        className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent ${
          isLoading
            ? 'border-indigo-300 bg-indigo-50 placeholder:text-indigo-400'
            : 'border-gray-300 placeholder:text-gray-500'
        }`}
        disabled={isLoading}
      />
      {isLoading && (
        <div className="absolute right-3 top-1/2 transform -translate-y-1/2">
          <LoadingSpinner size="sm" />
        </div>
      )}
    </div>
    <button
      type="submit"
      disabled={!query.trim() || isLoading}
      className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 min-w-[80px] justify-center"
    >
      {isLoading ? <LoadingSpinner size="sm" /> : (
        <>
          <SendIcon />
          Ask
        </>
      )}
    </button>
  </div>
</form>
```

## Step 43.8: Test the Query Assistant Interface

*Test the complete frontend integration with various query scenarios.*

### 43.8.1: Start the Development Environment

Start both the backend and frontend:

```bash
# Terminal 1: Start the backend API
cd src/BudgetTracker.Api
dotnet run

# Terminal 2: Start the React frontend
cd src/BudgetTracker.Web
npm run dev
```

### 43.8.2: Test Authentication Flow

1. Navigate to http://localhost:5173
2. Register or login with an account
3. Verify you can access the dashboard

### 43.8.3: Test Query Processing

Test with sample queries (requires transaction data):

```text
1. "What was my biggest expense this month?"
2. "Show me all coffee purchases"
3. "How much did I spend on groceries?"
4. "Find transactions over €100"
```

**Expected Results:**
- ✅ **Loading Feedback**: Spinner shows during processing
- ✅ **Response Display**: AI answers appear in chat-like format
- ✅ **Transaction Cards**: Relevant transactions are displayed with proper formatting
- ✅ **Error Handling**: Graceful error messages for failed queries
- ✅ **Currency Formatting**: Amounts display with proper currency symbols
- ✅ **Date Formatting**: Dates are human-readable

---

## Summary ✅

You've successfully built a complete frontend interface for the Natural Language Query Assistant:

✅ **Conversational Interface**: Chat-like UI for natural language financial queries
✅ **Real-time Processing**: Loading states and feedback during AI processing
✅ **Smart Suggestions**: Pre-built query suggestions to guide users
✅ **Rich Responses**: Formatted transaction displays with amounts and categories
✅ **Error Handling**: Robust error management and user feedback
✅ **Responsive Design**: Mobile-friendly interface with Tailwind CSS

**Key Features Implemented**:
- **Interactive Query Input**: Real-time input with suggestions and validation
- **Loading States**: Visual feedback during AI processing with spinners
- **Transaction Display**: Rich formatting for transaction results with categories
- **Error Management**: Toast notifications and graceful error handling
- **Accessibility**: Keyboard navigation and screen reader support
- **Mobile Responsive**: Works well on all device sizes

**Technical Achievements**:
- **TypeScript Integration**: Full type safety for API interactions
- **Component Architecture**: Reusable components following React best practices
- **State Management**: Proper state handling for async operations
- **API Integration**: Robust API client with error handling and timeouts
- **UI/UX Design**: Intuitive conversational interface patterns

**What Users Get**:
- **Natural Interaction**: Ask questions in plain English about their finances
- **Instant Insights**: Get immediate answers with relevant transaction details
- **Visual Clarity**: Clear, formatted display of financial information
- **Guided Experience**: Suggested queries help users discover capabilities
- **Reliable Operation**: Graceful handling of errors and edge cases

**User Experience Flow**:
1. **Discovery**: Users see suggested queries to understand capabilities
2. **Input**: Natural language input with real-time validation
3. **Processing**: Clear loading states during AI analysis
4. **Results**: Rich, formatted responses with transaction details
5. **Continuation**: Easy way to ask follow-up questions

The frontend now provides an intuitive, conversational interface that makes complex financial data exploration as simple as asking a question! 🎉