import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useToast } from '../../../shared/contexts/ToastContext';
import { apiClient } from '../../../api';
import { transactionsApi, type EnhanceImportResult } from '../api';
import type { ImportResult } from '../types';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';

type Step = 'upload' | 'imported' | 'enhanced' | 'complete';

interface FileUploadProps {
  className?: string;
}

function FileUpload({ className = '' }: FileUploadProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [account, setAccount] = useState('');
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [currentStep, setCurrentStep] = useState<Step>('upload');
  const [currentPhase, setCurrentPhase] = useState<'uploading' | 'detecting' | 'parsing' | 'extracting' | 'enhancing' | 'complete'>('uploading');
  const [minConfidenceScore, setMinConfidenceScore] = useState(0.7);
  const [enhanceResult, setEnhanceResult] = useState<EnhanceImportResult | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const { showSuccess, showError } = useToast();


  useEffect(() => {
    // Fetch XSRF token when component mounts to enable API calls
    const fetchXsrfToken = async () => {
      try {
        await apiClient.get('/antiforgery/token');
      } catch (error) {
        console.error('Failed to fetch XSRF token:', error);
      }
    };
    fetchXsrfToken();
  }, []);

  const validateFile = (file: File): string | null => {
    const validExtensions = ['.csv', '.png', '.jpg', '.jpeg'];
    const fileName = file.name.toLowerCase();

    if (!validExtensions.some(ext => fileName.endsWith(ext))) {
      return 'Please select a CSV file or bank statement image (PNG, JPG, JPEG)';
    }

    const maxSize = 10 * 1024 * 1024; // 10MB
    if (file.size > maxSize) {
      return 'File size must be less than 10MB';
    }

    return null;
  };

  const handleFileSelect = useCallback((file: File) => {
    const error = validateFile(file);
    if (error) {
      showError('Invalid File', error);
      return;
    }
    setSelectedFile(file);
    setImportResult(null);
  }, [showError]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);

    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) {
      handleFileSelect(files[0]);
    }
  }, [handleFileSelect]);

  const handleFileInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleFileSelect(files[0]);
    }
  }, [handleFileSelect]);

  const handleBrowseClick = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  const handleImport = useCallback(async () => {
    if (!selectedFile || !account.trim()) {
      return;
    }

    setIsUploading(true);
    setUploadProgress(0);
    setImportResult(null);
    setCurrentPhase('uploading');

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);
      formData.append('account', account.trim());

      // Determine processing phase based on file type
      const isImage = selectedFile.name.toLowerCase().match(/\.(png|jpg|jpeg)$/);

      const result = await transactionsApi.importTransactions({
        formData,
        onUploadProgress: (progressEvent) => {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          setUploadProgress(progress);

          // Update phase based on progress and file type
          if (progress < 20) {
            setCurrentPhase('uploading');
          } else if (progress < 60) {
            setCurrentPhase(isImage ? 'extracting' : 'detecting');
          } else if (progress < 85 && !isImage) {
            setCurrentPhase('parsing');
          } else if (progress < 100) {
            setCurrentPhase('enhancing');
          } else {
            setCurrentPhase('complete');
          }
        }
      });

      setImportResult(result);
      setCurrentStep('imported');

      showSuccess(
        `Successfully imported ${result.importedCount} transactions from ${getFileTypeLabel(selectedFile.name).toLowerCase()} with AI description enhancements ready for review`
      );
    } catch (error) {
      console.error('Import error:', error);

      // Extract error message from the response
      let errorMessage = 'Failed to import the file';
      if (error && typeof error === 'object' && 'message' in error) {
        errorMessage = (error as Error).message;
      }

      showError('Import Failed', errorMessage);
    } finally {
      setIsUploading(false);
      setUploadProgress(0);
      setCurrentPhase('uploading');
    }
  }, [selectedFile, account, showError, showSuccess]);

  const handleClearFile = useCallback(() => {
    setSelectedFile(null);
    setImportResult(null);
    setEnhanceResult(null);
    setCurrentStep('upload');
    setCurrentPhase('uploading');
    setUploadProgress(0);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, []);


  const handleEnhance = useCallback(async (applyEnhancements: boolean) => {
    if (!importResult) return;

    setIsUploading(true);

    try {
      const result = await transactionsApi.enhanceImport({
        importSessionHash: importResult.importSessionHash,
        enhancements: importResult.enhancements,
        minConfidenceScore,
        applyEnhancements
      });

      setEnhanceResult(result);
      setCurrentStep(applyEnhancements ? 'enhanced' : 'complete');

      if (applyEnhancements) {
        showSuccess(
          `Successfully enhanced ${result.enhancedCount} out of ${result.totalTransactions} transactions`
        );
      } else {
        showSuccess('Import Complete - Transactions imported without AI enhancement');
      }

      // Redirect to transactions page after a short delay
      setTimeout(() => {
        navigate('/transactions');
      }, 6000);
    } catch (error) {
      showError('Enhancement Failed', 'Failed to enhance transactions');
      console.error('Enhancement error:', error);
    } finally {
      setIsUploading(false);
    }
  }, [importResult, minConfidenceScore, showSuccess, showError, navigate]);

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getFileTypeIcon = (fileName: string) => {
    const name = fileName.toLowerCase();
    if (name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg')) {
      return '🖼️'; // Image icon
    }
    return '📊'; // CSV/spreadsheet icon
  };

  const getFileTypeLabel = (fileName: string) => {
    const name = fileName.toLowerCase();
    if (name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg')) {
      return 'Bank Statement Image';
    }
    return 'CSV Bank Statement';
  };

  const getPhaseDescription = (phase: string, fileName: string): string => {
    const isImage = fileName.toLowerCase().match(/\.(png|jpg|jpeg)$/);

    switch (phase) {
      case 'uploading':
        return `Uploading ${isImage ? 'bank statement image' : 'CSV file'}...`;
      case 'detecting':
        return isImage ? 'Preparing image for analysis...' : 'Detecting CSV structure...';
      case 'parsing':
        return 'Parsing CSV data...';
      case 'extracting':
        return 'Extracting transactions from image using AI...';
      case 'enhancing':
        return 'Enhancing transaction descriptions with AI...';
      case 'complete':
        return 'Import completed successfully!';
      default:
        return 'Processing...';
    }
  };

  return (
    <div className={`space-y-8 ${className}`}>
      {/* Step Indicator */}
      <div className="flex items-center justify-center space-x-8">
        <div className={`flex items-center space-x-2 ${currentStep === 'upload' ? 'text-blue-600' : currentStep === 'imported' || currentStep === 'enhanced' || currentStep === 'complete' ? 'text-green-600' : 'text-gray-400'}`}>
          <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${currentStep === 'upload' ? 'bg-blue-100 text-blue-600' : currentStep === 'imported' || currentStep === 'enhanced' || currentStep === 'complete' ? 'bg-green-100 text-green-600' : 'bg-gray-100 text-gray-400'}`}>
            1
          </div>
          <span className="text-sm font-medium">Upload</span>
        </div>
        <div className={`w-16 h-0.5 ${currentStep === 'imported' || currentStep === 'enhanced' || currentStep === 'complete' ? 'bg-green-600' : 'bg-gray-200'}`}></div>
        <div className={`flex items-center space-x-2 ${currentStep === 'imported' ? 'text-blue-600' : currentStep === 'enhanced' || currentStep === 'complete' ? 'text-green-600' : 'text-gray-400'}`}>
          <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${currentStep === 'imported' ? 'bg-blue-100 text-blue-600' : currentStep === 'enhanced' || currentStep === 'complete' ? 'bg-green-100 text-green-600' : 'bg-gray-100 text-gray-400'}`}>
            2
          </div>
          <span className="text-sm font-medium">Import</span>
        </div>
        <div className={`w-16 h-0.5 ${currentStep === 'enhanced' || currentStep === 'complete' ? 'bg-green-600' : 'bg-gray-200'}`}></div>
        <div className={`flex items-center space-x-2 ${currentStep === 'enhanced' || currentStep === 'complete' ? 'text-green-600' : 'text-gray-400'}`}>
          <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${currentStep === 'enhanced' || currentStep === 'complete' ? 'bg-green-100 text-green-600' : 'bg-gray-100 text-gray-400'}`}>
            3
          </div>
          <span className="text-sm font-medium">Complete</span>
        </div>
      </div>

      {/* Step 1: File Upload */}
      {currentStep === 'upload' && (
        <>
          <div
            className={`relative border-2 border-dashed rounded-2xl p-10 text-center transition-all duration-300 ${
              isDragOver
                ? 'border-blue-400 bg-blue-50 scale-[1.02]'
                : 'border-gray-300 hover:border-blue-300 hover:bg-blue-25'
            }`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
          >
            {/* Upload area content */}
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv,.png,.jpg,.jpeg"
              onChange={handleFileInputChange}
              className="hidden"
            />

            <div className="space-y-6">
              <div className={`mx-auto h-16 w-16 transition-all duration-200 ${isDragOver ? 'text-blue-500 scale-110' : 'text-gray-400'}`}>
                <svg fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={1.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
                </svg>
              </div>

              <div className="space-y-2">
                <p className="text-xl font-semibold text-gray-900">
                  {selectedFile ? 'File Selected' : 'Upload Bank Statement'}
                </p>
                <p className="text-sm text-gray-600 leading-relaxed">
                  {selectedFile
                    ? `${selectedFile.name} (${formatFileSize(selectedFile.size)})`
                    : 'Drag and drop your bank statement here, or click to browse'
                  }
                </p>
                {!selectedFile && (
                  <div className="flex items-center justify-center space-x-2 mt-3">
                    <div className="flex items-center space-x-1 text-xs text-blue-600 bg-blue-50 px-2 py-1 rounded-full">
                      <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                      </svg>
                      <span>Smart Detection</span>
                    </div>
                    <div className="flex items-center space-x-1 text-xs text-green-600 bg-green-50 px-2 py-1 rounded-full">
                      <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3.055 11H5a2 2 0 012 2v1a2 2 0 002 2 2 2 0 012 2v2.945M8 3.935V5.5A2.5 2.5 0 0010.5 8h.5a2 2 0 012 2 2 2 0 104 0 2 2 0 012-2h1.064M15 20.488V18a2 2 0 012-2h3.064M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      <span>Multi-Language</span>
                    </div>
                  </div>
                )}
              </div>

              {!selectedFile && (
                <button
                  onClick={handleBrowseClick}
                  className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-xl text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out"
                >
                  Browse Files
                </button>
              )}
            </div>
          </div>

          {selectedFile && (
            <div className="bg-blue-50 rounded-2xl p-6 border border-blue-100">
              <div className="space-y-4">
                <div className="flex items-center space-x-4">
                  <div className="flex-shrink-0 p-2 bg-green-100 rounded-xl">
                    <span className="text-green-600 text-lg">{getFileTypeIcon(selectedFile.name)}</span>
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-gray-900">{selectedFile.name}</p>
                    <p className="text-xs text-gray-600">{getFileTypeLabel(selectedFile.name)} • {formatFileSize(selectedFile.size)}</p>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label htmlFor="account" className="text-sm font-medium text-gray-700 block mb-2">
                      Account Name
                    </label>
                    <input
                      type="text"
                      id="account"
                      value={account}
                      onChange={(e) => setAccount(e.target.value)}
                      placeholder="Enter account name (e.g. Checking, Savings)"
                      className="w-full px-3 py-2 border border-gray-300 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    />
                  </div>

                  <div>
                    <label htmlFor="confidence" className="text-sm font-medium text-gray-700 block mb-2">
                      AI Confidence Threshold
                    </label>
                    <select
                      id="confidence"
                      value={minConfidenceScore}
                      onChange={(e) => setMinConfidenceScore(Number(e.target.value))}
                      className="w-full px-3 py-2 border border-gray-300 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    >
                      <option value={0.3}>Low (30%) - More changes</option>
                      <option value={0.5}>Medium (50%) - Balanced</option>
                      <option value={0.7}>High (70%) - Conservative</option>
                    </select>
                  </div>
                </div>

                <div className="flex items-center justify-end space-x-3">
                  <button
                    onClick={handleClearFile}
                    disabled={isUploading}
                    className="inline-flex items-center justify-center px-3 py-2 border border-gray-300 text-xs font-medium rounded-xl text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out disabled:opacity-50"
                  >
                    Remove
                  </button>
                  <button
                    onClick={handleImport}
                    disabled={isUploading || !account.trim()}
                    className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-xl text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isUploading ? (
                      <>
                        <LoadingSpinner size="sm" />
                        <span className="ml-2">
                          {getPhaseDescription(currentPhase, selectedFile.name)}
                        </span>
                      </>
                    ) : (
                      'Import Transactions'
                    )}
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Enhanced Upload Progress */}
          {isUploading && uploadProgress > 0 && (
            <div className="space-y-2">
              <div className="flex justify-between text-sm font-medium">
                <span className="text-gray-700">
                  {selectedFile ? getPhaseDescription(currentPhase, selectedFile.name) : 'Processing...'}
                </span>
                <span className="text-blue-600">{uploadProgress}%</span>
              </div>
              <div className="w-full bg-gray-200 rounded-full h-3 overflow-hidden">
                <div
                  className="h-3 rounded-full transition-all duration-500 ease-out bg-gradient-to-r from-blue-500 to-blue-600"
                  style={{ width: `${uploadProgress}%` }}
                />
              </div>
            </div>
          )}
        </>
      )}

      {/* Step 2: Review Enhanced Transactions */}
      {currentStep === 'imported' && importResult && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-semibold text-gray-900">Review AI Enhancements</h3>
              <div className="flex items-center space-x-4 text-sm">
                <span className="text-green-600">
                  {importResult.importedCount} transactions imported
                </span>
                <span className="text-blue-600">
                  {importResult.enhancements.filter((e: any) => e.confidenceScore >= minConfidenceScore).length} will be enhanced
                </span>
              </div>
            </div>

            <div className="space-y-3 max-h-96 overflow-y-auto">
              {importResult.enhancements.slice(0, 10).map((enhancement: any, index: number) => {
                const willEnhance = enhancement.confidenceScore >= minConfidenceScore;

                return (
                  <div key={`${importResult.importSessionHash}-${index}`} className="border border-gray-200 rounded-lg p-4">
                    <div className="flex justify-between items-start">
                      <div className="flex-1 space-y-2">
                        <div className="flex items-center justify-between">
                          <span className="text-sm text-gray-500">
                            Transaction #{index + 1}
                          </span>
                          <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                            enhancement.confidenceScore >= 0.8 ? 'bg-green-100 text-green-800' :
                              enhancement.confidenceScore >= 0.6 ? 'bg-yellow-100 text-yellow-800' :
                                'bg-red-100 text-red-800'
                          }`}>
                            {Math.round(enhancement.confidenceScore * 100)}% confidence
                          </span>
                        </div>

                        <div className="space-y-1">
                          <div className="text-sm text-gray-500">
                            <span className="font-medium">Original:</span> {enhancement.originalDescription}
                          </div>
                          {willEnhance && (
                            <div className="text-sm font-medium text-gray-900">
                              <span className="font-medium text-green-700">Enhanced:</span> {enhancement.enhancedDescription}
                              {enhancement.suggestedCategory && (
                                <span className="ml-2 inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700">
                                  {enhancement.suggestedCategory}
                                </span>
                              )}
                            </div>
                          )}
                          {!willEnhance && (
                            <div className="text-sm text-gray-400 italic">
                              Enhancement skipped (confidence below {Math.round(minConfidenceScore * 100)}%)
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}

              {importResult.enhancements.length > 10 && (
                <div className="text-center py-3 text-sm text-gray-500">
                  ... and {importResult.enhancements.length - 10} more transactions
                </div>
              )}
            </div>

            <div className="mt-6 pt-6 border-t border-gray-200">
              <div className="flex items-center justify-between">
                <div className="space-y-1">
                  <label className="block text-sm font-medium text-gray-700">
                    AI Confidence Threshold
                  </label>
                  <p className="text-xs text-gray-500">
                    Only enhancements with confidence above this threshold will be applied
                  </p>
                </div>
                <div className="flex items-center space-x-3">
                  <span className="text-sm text-gray-600">{Math.round(minConfidenceScore * 100)}%</span>
                  <input
                    type="range"
                    min="0.3"
                    max="0.9"
                    step="0.1"
                    value={minConfidenceScore}
                    onChange={(e) => setMinConfidenceScore(parseFloat(e.target.value))}
                    className="w-24"
                  />
                </div>
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between">
            <button
              onClick={handleClearFile}
              className="inline-flex items-center justify-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-xl text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out"
            >
              Start Over
            </button>

            <div className="flex items-center space-x-3">
              <button
                onClick={() => handleEnhance(false)}
                disabled={isUploading}
                className="inline-flex items-center justify-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-xl text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out disabled:opacity-50"
              >
                Skip Enhancement
              </button>
              <button
                onClick={() => handleEnhance(true)}
                disabled={isUploading}
                className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-xl text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 transition-all duration-200 ease-in-out disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isUploading ? (
                  <>
                    <LoadingSpinner size="sm" />
                    <span className="ml-2">Enhancing...</span>
                  </>
                ) : (
                  'Enhance with AI'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Step 3: Enhanced Complete */}
      {currentStep === 'enhanced' && enhanceResult && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6">
          <div className="text-center space-y-6">
            <div className="mx-auto w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
              <span className="text-green-600 text-2xl">✓</span>
            </div>
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">Enhancement Complete!</h3>
              <p className="text-gray-600">
                Successfully enhanced {enhanceResult.enhancedCount} out of {enhanceResult.totalTransactions} transactions.
              </p>
            </div>
            <button
              onClick={() => navigate('/transactions')}
              className="inline-flex items-center justify-center px-6 py-3 border border-transparent text-base font-medium rounded-xl text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out"
            >
              View Transactions
            </button>
          </div>
        </div>
      )}

      {/* Step 3: Complete without enhancement */}
      {currentStep === 'complete' && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6">
          <div className="text-center space-y-6">
            <div className="mx-auto w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
              <span className="text-green-600 text-2xl">✓</span>
            </div>
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">Import Complete!</h3>
              <p className="text-gray-600">
                Your transactions have been imported successfully.
              </p>
            </div>
            <button
              onClick={() => navigate('/transactions')}
              className="inline-flex items-center justify-center px-6 py-3 border border-transparent text-base font-medium rounded-xl text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all duration-200 ease-in-out"
            >
              View Transactions
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default FileUpload;