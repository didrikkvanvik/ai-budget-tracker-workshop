import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { getCategoryColor } from '../../../shared/utils/formatters';
import { transactionsApi } from '../api';
import type { TransactionFilters as FilterData } from '../types';

const FilterIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
  </svg>
);

const XIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="6" x2="6" y2="18" />
    <line x1="6" y1="6" x2="18" y2="18" />
  </svg>
);

const TrashIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 6h18" />
    <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6" />
    <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2" />
    <line x1="10" y1="11" x2="10" y2="17" />
    <line x1="14" y1="11" x2="14" y2="17" />
  </svg>
);

interface TransactionFiltersProps {
  selectedCount?: number;
  totalCount?: number;
  allSelected?: boolean;
  onSelectAll?: () => void;
  onDelete?: () => void;
}

export default function TransactionFilters({
  selectedCount = 0,
  allSelected = false,
  onSelectAll,
  onDelete
}: TransactionFiltersProps) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [filters, setFilters] = useState<FilterData>({ categories: [], accounts: [] });
  const [isLoading, setIsLoading] = useState(true);

  const selectedCategory = searchParams.get('category') || '';
  const selectedAccount = searchParams.get('account') || '';

  useEffect(() => {
    const loadFilters = async () => {
      try {
        const data = await transactionsApi.getFilters();
        setFilters(data);
      } catch (error) {
        console.error('Failed to load filters:', error);
      } finally {
        setIsLoading(false);
      }
    };

    loadFilters();
  }, []);

  const handleCategoryChange = (category: string) => {
    const newParams = new URLSearchParams(searchParams);
    if (category) {
      newParams.set('category', category);
    } else {
      newParams.delete('category');
    }
    newParams.delete('page'); // Reset to first page when filtering
    setSearchParams(newParams);
  };

  const handleAccountChange = (account: string) => {
    const newParams = new URLSearchParams(searchParams);
    if (account) {
      newParams.set('account', account);
    } else {
      newParams.delete('account');
    }
    newParams.delete('page'); // Reset to first page when filtering
    setSearchParams(newParams);
  };

  const clearFilters = () => {
    const newParams = new URLSearchParams(searchParams);
    newParams.delete('category');
    newParams.delete('account');
    newParams.delete('page');
    setSearchParams(newParams);
  };

  const hasActiveFilters = selectedCategory || selectedAccount;

  if (isLoading) {
    return (
      <div className="bg-white rounded-lg border border-neutral-200 p-4 mb-4">
        <div className="flex items-center gap-3">
          <div className="h-4 w-4 rounded bg-neutral-200 animate-pulse" />
          <div className="h-8 w-48 rounded bg-neutral-200 animate-pulse" />
          <div className="h-8 w-48 rounded bg-neutral-200 animate-pulse" />
        </div>
      </div>
    );
  }

  const hasSelection = selectedCount > 0;

  return (
    <div className="bg-white rounded-lg border border-neutral-200 p-4 mb-4 shadow-sm">
      <div className="flex flex-wrap items-center gap-3">
        {/* Select All Checkbox */}
        {onSelectAll && (
          <div className="flex items-center gap-2 pr-3 border-r border-neutral-200">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={onSelectAll}
              className="w-4 h-4 text-indigo-600 border-neutral-300 rounded focus:ring-indigo-500 focus:ring-2 cursor-pointer"
              aria-label="Select all transactions"
            />
            <span className="text-sm font-medium text-gray-700">
              {hasSelection ? `${selectedCount} selected` : 'Select all'}
            </span>
          </div>
        )}

        {/* Delete Button */}
        {hasSelection && onDelete && (
          <button
            onClick={onDelete}
            className="cursor-pointer flex items-center gap-2 px-3 py-1.5 bg-red-600 text-white text-sm font-medium rounded-md hover:bg-red-700 transition-colors focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2"
          >
            <TrashIcon />
            Delete {selectedCount} {selectedCount === 1 ? 'transaction' : 'transactions'}
          </button>
        )}

        {/* Filters Section - only show if no items selected */}
        {!hasSelection && (
          <>
            <div className="flex items-center gap-2 text-sm font-medium text-gray-700">
              <FilterIcon />
              <span>Filter by:</span>
            </div>

        {/* Category Filter */}
        <div className="relative">
          <select
            value={selectedCategory}
            onChange={(e) => handleCategoryChange(e.target.value)}
            className="appearance-none bg-white border border-neutral-300 rounded-md px-4 py-2 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent hover:border-neutral-400 transition-colors cursor-pointer"
          >
            <option value="">All Categories</option>
            {filters.categories.map((category) => (
              <option key={category} value={category}>
                {category}
              </option>
            ))}
          </select>
          <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-gray-500">
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>

        {/* Account Filter */}
        <div className="relative">
          <select
            value={selectedAccount}
            onChange={(e) => handleAccountChange(e.target.value)}
            className="appearance-none bg-white border border-neutral-300 rounded-md px-4 py-2 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent hover:border-neutral-400 transition-colors cursor-pointer"
          >
            <option value="">All Accounts</option>
            {filters.accounts.map((account) => (
              <option key={account} value={account}>
                {account}
              </option>
            ))}
          </select>
          <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-gray-500">
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>

        {/* Active Filters & Clear Button */}
        {hasActiveFilters && (
          <>
            <div className="flex-1" />
            <div className="flex items-center gap-2">
              {selectedCategory && (
                <span className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium ${getCategoryColor(selectedCategory)}`}>
                  {selectedCategory}
                  <button
                    onClick={() => handleCategoryChange('')}
                    className="cursor-pointer hover:bg-black/10 rounded p-0.5 transition-colors"
                    aria-label="Clear category filter"
                  >
                    <XIcon />
                  </button>
                </span>
              )}
              {selectedAccount && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium bg-blue-100 text-blue-700">
                  {selectedAccount}
                  <button
                    onClick={() => handleAccountChange('')}
                    className="cursor-pointer hover:bg-black/10 rounded p-0.5 transition-colors"
                    aria-label="Clear account filter"
                  >
                    <XIcon />
                  </button>
                </span>
              )}
              <button
                onClick={clearFilters}
                className="cursor-pointer text-sm text-gray-600 hover:text-gray-900 font-medium underline transition-colors"
              >
                Clear all
              </button>
            </div>
          </>
        )}
          </>
        )}
      </div>
    </div>
  );
}
