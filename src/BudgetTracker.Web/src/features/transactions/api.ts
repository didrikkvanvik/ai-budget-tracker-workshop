import { apiClient } from '../../api';
import type {
  TransactionListDto,
  GetTransactionsParams,
  ImportTransactionsParams,
  ImportResult,
  EnhanceImportRequest,
  EnhanceImportResult,
  TransactionEnhancementResult
} from './types';

export type { EnhanceImportResult, TransactionEnhancementResult };

function handleError(message: string, error: any): void {
  console.error(message, error);
  throw new Error(message);
}

export const transactionsApi = {
  async getTransactions(params: GetTransactionsParams = {}): Promise<TransactionListDto> {
    const { page = 1, pageSize = 20 } = params;
    const response = await apiClient.get<TransactionListDto>('/transactions', {
      params: { page, pageSize }
    });
    return response.data;
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
  }
};