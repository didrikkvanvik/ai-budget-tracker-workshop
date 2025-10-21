import { useState } from 'react';
import { getCategoryColor } from '../../../shared/utils/formatters';

interface CategoryManagerProps {
  transactionId?: string;
  existingCategories?: string[];
  availableCategories?: string[];
  onAddCategory?: (category: string) => void | Promise<void>;
  onRemoveCategory?: (category: string) => void | Promise<void>;
  disabled?: boolean;
  compact?: boolean;
}

const PlusIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" y1="5" x2="12" y2="19"></line>
    <line x1="5" y1="12" x2="19" y2="12"></line>
  </svg>
);

const XIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="6" x2="6" y2="18"></line>
    <line x1="6" y1="6" x2="18" y2="18"></line>
  </svg>
);

export default function CategoryManager({
  existingCategories = [],
  availableCategories = [],
  onAddCategory,
  onRemoveCategory,
  disabled = false,
  compact = false
}: CategoryManagerProps) {
  const [newCategory, setNewCategory] = useState('');
  const [isAdding, setIsAdding] = useState(false);
  const [showInput, setShowInput] = useState(false);

  const handleAddCategory = async () => {
    const categoryToAdd = newCategory.trim();
    if (!categoryToAdd || !onAddCategory) return;

    if (existingCategories.includes(categoryToAdd)) {
      return; // Already exists
    }

    setIsAdding(true);
    try {
      await onAddCategory(categoryToAdd);
      setNewCategory('');
      setShowInput(false);
    } catch (error) {
      console.error('Failed to add category:', error);
    } finally {
      setIsAdding(false);
    }
  };

  const handleRemoveCategory = async (category: string) => {
    if (!onRemoveCategory || disabled) return;

    try {
      await onRemoveCategory(category);
    } catch (error) {
      console.error('Failed to remove category:', error);
    }
  };

  const handleSelectExisting = async (category: string) => {
    if (!onAddCategory || disabled || existingCategories.includes(category)) return;

    setIsAdding(true);
    try {
      await onAddCategory(category);
    } catch (error) {
      console.error('Failed to add category:', error);
    } finally {
      setIsAdding(false);
    }
  };

  const suggestedCategories = availableCategories.filter(
    cat => !existingCategories.includes(cat)
  );

  return (
    <div className={`space-y-2 ${compact ? 'text-xs' : 'text-sm'}`}>
      {/* Existing Categories */}
      {existingCategories.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {existingCategories.map((category) => (
            <span
              key={category}
              className={`inline-flex items-center gap-1 px-2 py-1 rounded ${
                compact ? 'text-xs' : 'text-sm'
              } font-medium ${getCategoryColor(category)} ${
                !disabled && onRemoveCategory ? 'pr-1' : ''
              }`}
            >
              {category}
              {!disabled && onRemoveCategory && (
                <button
                  type="button"
                  onClick={() => handleRemoveCategory(category)}
                  className="cursor-pointer ml-1 hover:bg-black/10 rounded p-0.5 transition-colors"
                  aria-label={`Remove ${category}`}
                >
                  <XIcon />
                </button>
              )}
            </span>
          ))}
        </div>
      )}

      {/* Add Category Section */}
      {!disabled && onAddCategory && (
        <div className="space-y-2">
          {!showInput ? (
            <button
              type="button"
              onClick={() => setShowInput(true)}
              disabled={isAdding}
              className={`cursor-pointer inline-flex items-center gap-1 px-2 py-1 rounded ${
                compact ? 'text-xs' : 'text-sm'
              } font-medium border border-dashed border-gray-300 text-gray-600 hover:border-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors disabled:opacity-50`}
            >
              <PlusIcon />
              Add Category
            </button>
          ) : (
            <div className="flex gap-2">
              <input
                type="text"
                value={newCategory}
                onChange={(e) => setNewCategory(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    handleAddCategory();
                  } else if (e.key === 'Escape') {
                    setNewCategory('');
                    setShowInput(false);
                  }
                }}
                placeholder="Enter category name..."
                disabled={isAdding}
                autoFocus
                className={`flex-1 px-2 py-1 border border-gray-300 rounded ${
                  compact ? 'text-xs' : 'text-sm'
                } focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:opacity-50`}
              />
              <button
                type="button"
                onClick={handleAddCategory}
                disabled={!newCategory.trim() || isAdding}
                className={`cursor-pointer px-3 py-1 bg-indigo-600 text-white rounded ${
                  compact ? 'text-xs' : 'text-sm'
                } font-medium hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors`}
              >
                {isAdding ? 'Adding...' : 'Add'}
              </button>
              <button
                type="button"
                onClick={() => {
                  setNewCategory('');
                  setShowInput(false);
                }}
                disabled={isAdding}
                className={`cursor-pointer px-3 py-1 border border-gray-300 text-gray-700 rounded ${
                  compact ? 'text-xs' : 'text-sm'
                } font-medium hover:bg-gray-50 disabled:opacity-50 transition-colors`}
              >
                Cancel
              </button>
            </div>
          )}

          {/* Suggested Categories */}
          {!showInput && suggestedCategories.length > 0 && (
            <div className="space-y-1">
              <p className={`text-gray-600 ${compact ? 'text-xs' : 'text-sm'}`}>
                Suggested:
              </p>
              <div className="flex flex-wrap gap-1">
                {suggestedCategories.slice(0, 5).map((category) => (
                  <button
                    key={category}
                    type="button"
                    onClick={() => handleSelectExisting(category)}
                    disabled={isAdding}
                    className={`cursor-pointer inline-flex items-center gap-1 px-2 py-1 rounded ${
                      compact ? 'text-xs' : 'text-sm'
                    } font-medium border border-gray-200 text-gray-600 hover:border-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors disabled:opacity-50`}
                  >
                    <PlusIcon />
                    {category}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
