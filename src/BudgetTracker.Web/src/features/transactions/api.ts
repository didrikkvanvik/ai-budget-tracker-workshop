import { apiClient } from '../../api';
import type {
  TransactionListDto,
  GetTransactionsParams,
  ImportTransactionsParams,
  ImportResult,
  EnhanceImportRequest,
  EnhanceImportResult,
  TransactionEnhancementResult,
  TransactionFilters,
  DeleteTransactionsRequest,
  DeleteTransactionsResult
} from './types';

export type { EnhanceImportResult, TransactionEnhancementResult };

function handleError(message: string, error: any): void {
  console.error(message, error);
  throw new Error(message);
}

export const transactionsApi = {
  async getTransactions(params: GetTransactionsParams = {}): Promise<TransactionListDto> {
    const { page = 1, pageSize = 20, category, account } = params;
    const response = await apiClient.get<TransactionListDto>('/transactions', {
      params: { page, pageSize, category, account }
    });
    return response.data;
  },

  async getFilters(): Promise<TransactionFilters> {
    const response = await apiClient.get<TransactionFilters>('/transactions/filters');
    return response.data;
  },

  async deleteTransactions(request: DeleteTransactionsRequest): Promise<DeleteTransactionsResult> {
    try {
      const response = await apiClient.delete<DeleteTransactionsResult>('/transactions/bulk', {
        data: request
      });
      return response.data;
    } catch (error) {
      handleError('Failed to delete transactions', error);
      throw error;
    }
  },

  async importTransactions(params: ImportTransactionsParams): Promise<ImportResult> {
    try {
      const response = await apiClient.post<ImportResult>('/transactions/import', params.formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: params.onUploadProgress
      });
      return response.data;
    } catch (error) {
      handleError('Failed to import transactions', error);
      throw error;
    }
  },

  async enhanceImport(request: EnhanceImportRequest): Promise<EnhanceImportResult> {
    try {
      const response = await apiClient.post<EnhanceImportResult>('/transactions/import/enhance', request);
      return response.data;
    } catch (error) {
      handleError('Failed to enhance transactions', error);
      throw error;
    }
  },

  async addCategory(transactionId: string, categoryName: string): Promise<{ id: string; categoryName: string }> {
    try {
      const response = await apiClient.post(`/transactions/${transactionId}/categories`, { categoryName });
      return response.data;
    } catch (error) {
      handleError('Failed to add category', error);
      throw error;
    }
  },

  async removeCategory(transactionId: string, categoryName: string): Promise<void> {
    try {
      await apiClient.delete(`/transactions/${transactionId}/categories/${encodeURIComponent(categoryName)}`);
    } catch (error) {
      handleError('Failed to remove category', error);
      throw error;
    }
  },

  async addBulkCategories(transactionIds: string[], categoryNames: string[]): Promise<{ addedCount: number; message: string }> {
    try {
      const response = await apiClient.post('/transactions/bulk-categories', { transactionIds, categoryNames });
      return response.data;
    } catch (error) {
      handleError('Failed to add categories in bulk', error);
      throw error;
    }
  }
};