import { useEffect, useState } from 'react';
import { useLoaderData, useNavigation, useRevalidator } from 'react-router-dom';
import EmptyState from '../../../shared/components/EmptyState';
import Pagination from '../../../shared/components/Pagination';
import { SkeletonCardRow } from '../../../shared/components/Skeleton';
import { useToast } from '../../../shared/contexts/ToastContext';
import { formatDate, getCategoryColor } from '../../../shared/utils/formatters';
import { transactionsApi } from '../api';
import type { TransactionListDto } from '../types';
import CategoryManager from './CategoryManager';
import TransactionFilters from './TransactionFilters';

export default function TransactionList() {
  const data = useLoaderData() as TransactionListDto;
  const navigation = useNavigation();
  const revalidator = useRevalidator();
  const { showToast } = useToast();
  const isLoading = navigation.state === 'loading';
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isDeleting, setIsDeleting] = useState(false);
  const [availableCategories, setAvailableCategories] = useState<string[]>([]);
  const [expandedTransactionId, setExpandedTransactionId] = useState<string | null>(null);

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

  const handleSelectAll = () => {
    if (selectedIds.size === data.items.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(data.items.map(t => t.id)));
    }
  };

  const handleSelectTransaction = (id: string) => {
    const newSelected = new Set(selectedIds);
    if (newSelected.has(id)) {
      newSelected.delete(id);
    } else {
      newSelected.add(id);
    }
    setSelectedIds(newSelected);
  };

  const handleDelete = async () => {
    if (selectedIds.size === 0) return;

    setIsDeleting(true);
    try {
      const result = await transactionsApi.deleteTransactions({
        transactionIds: Array.from(selectedIds)
      });

      showToast('success', `Successfully deleted ${result.deletedCount} transaction${result.deletedCount === 1 ? '' : 's'}`);
      setSelectedIds(new Set());
      revalidator.revalidate();
    } catch (error) {
      console.error('Failed to delete transactions:', error);
      showToast('error', 'Failed to delete transactions. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  };

  useEffect(() => {
    const fetchCategories = async () => {
      try {
        const filters = await transactionsApi.getFilters();
        setAvailableCategories(filters.categories);
      } catch (error) {
        console.error('Failed to fetch categories:', error);
      }
    };
    fetchCategories();
  }, [data]);

  const handleAddCategory = async (transactionId: string, categoryName: string) => {
    try {
      await transactionsApi.addCategory(transactionId, categoryName);
      showToast('success', `Added category "${categoryName}"`);
      revalidator.revalidate();
    } catch (error) {
      console.error('Failed to add category:', error);
      showToast('error', 'Failed to add category. Please try again.');
    }
  };

  const handleRemoveCategory = async (transactionId: string, categoryName: string) => {
    try {
      await transactionsApi.removeCategory(transactionId, categoryName);
      showToast('success', `Removed category "${categoryName}"`);
      revalidator.revalidate();
    } catch (error) {
      console.error('Failed to remove category:', error);
      showToast('error', 'Failed to remove category. Please try again.');
    }
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
      <TransactionFilters
        selectedCount={selectedIds.size}
        totalCount={data.items.length}
        allSelected={selectedIds.size === data.items.length && data.items.length > 0}
        onSelectAll={handleSelectAll}
        onDelete={isDeleting ? undefined : handleDelete}
      />
      <div className="space-y-2">
        {data.items.map((transaction) => {
          const isSelected = selectedIds.has(transaction.id);
          const isExpanded = expandedTransactionId === transaction.id;
          const categories = (transaction?.categories ?? [])?.length > 1 ? transaction.categories?.filter((i) => i !== "Uncategorized") : transaction.categories;

          const deletableCategories = transaction.categories?.filter((i) => i !== "Uncategorized");

          return (
            <div
              key={transaction.id}
              className={`bg-white rounded-lg border p-4 transition-all duration-200 ${
                isSelected
                  ? 'border-indigo-300 ring-2 ring-indigo-100'
                  : 'border-neutral-100 hover:shadow-sm'
              }`}
            >
              <div className="flex items-start gap-3">
                {/* Checkbox */}
                <div className="flex items-center pt-0.5">
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() => handleSelectTransaction(transaction.id)}
                    className="w-4 h-4 text-indigo-600 border-neutral-300 rounded focus:ring-indigo-500 focus:ring-2 cursor-pointer"
                    aria-label={`Select transaction: ${transaction.description}`}
                  />
                </div>

                {/* Transaction Content */}
                <div className="flex-1 min-w-0 space-y-2">
                  <div className="flex justify-between items-start gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start gap-2">
                        <p className="text-sm font-medium text-gray-900 truncate">
                          {transaction.description}
                        </p>
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
                    <div className="flex items-center gap-3">
                      <div className="text-sm">
                        {formatAmount(transaction.amount)}
                      </div>
                      <button
                        onClick={() => setExpandedTransactionId(isExpanded ? null : transaction.id)}
                        className="cursor-pointer text-indigo-600 hover:text-indigo-700 text-xs font-medium transition-colors"
                      >
                        {isExpanded ? 'Hide' : 'Edit'} Categories
                      </button>
                    </div>
                  </div>

                  {/* Categories Section */}
                  {isExpanded ? (
                    <div className="pt-2 border-t border-gray-100">
                      <CategoryManager
                        transactionId={transaction.id}
                        existingCategories={deletableCategories}
                        availableCategories={availableCategories}
                        onAddCategory={(category) => handleAddCategory(transaction.id, category)}
                        onRemoveCategory={(category) => handleRemoveCategory(transaction.id, category)}
                        compact
                      />
                    </div>
                  ) : (
                    <div className="flex flex-wrap gap-1">
                      {(categories ?? []).map((cat, idx) => (
                          <span key={idx} className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${getCategoryColor(cat)}`}>
                            {cat}
                          </span>
                        ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })}
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