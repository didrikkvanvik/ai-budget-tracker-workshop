# Workshop Step 013: Multi-Step Transaction Upload

## Mission 🎯

In this step, you'll transform the basic file upload into a sophisticated multi-step workflow with progress tracking and enhanced user experience. This prepares the foundation for AI enhancement previews in the next step.

**Your goal**: Convert the simple single-step upload into a professional multi-step wizard with visual progress indicators and enhanced processing phases.

**Learning Objectives**:
- Building multi-step user workflows in React
- Managing complex state transitions and UI flows
- Creating professional upload experiences with progress tracking
- Setting up the foundation for AI enhancement workflows

---

## Prerequisites

Before starting, ensure you completed:
- [012-ai-transaction-enhancement-backend.md](012-ai-transaction-enhancement-backend.md) - AI backend implementation

---

## Step 13.1: Review Current Basic Implementation

*Let's understand the existing simple FileUpload component before building the multi-step enhancement process.*

Check the current basic implementation in `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`. You'll see it has:

- ✅ Basic drag-and-drop file upload for CSV files
- ✅ Account name input
- ✅ Simple upload progress indicator
- ✅ Basic success/error handling
- ✅ Automatic redirect to transactions page

This basic implementation works but provides no AI enhancement capabilities or user control over the process.

## Step 13.2: Add Multi-Step State Management

*Transform the component to support multiple steps in the import process.*

First, we need to add the new state and types for the multi-step process. Update the import statements and add new state variables:

**In `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`:**

```tsx
import { transactionsApi, type EnhanceImportResult, type ImportResult } from '../api';

type Step = 'upload' | 'imported' | 'enhanced' | 'complete';
```

Next, add the new state variables after the existing ones:

```tsx
const [currentStep, setCurrentStep] = useState<Step>('upload');
const [currentPhase, setCurrentPhase] = useState<'uploading' | 'detecting' | 'parsing' | 'converting' | 'extracting' | 'enhancing' | 'complete'>('uploading');
const [minConfidenceScore, setMinConfidenceScore] = useState(0.7);
const [enhanceResult, setEnhanceResult] = useState<EnhanceImportResult | null>(null);
```

## Step 13.3: Keep Simple CSV Validation

*The existing CSV-only validation is perfect for this workshop step.*

The existing validation function in the basic implementation already handles CSV files correctly:

```tsx
const validateFile = (file: File): string | null => {
  const fileName = file.name.toLowerCase();

  if (!fileName.endsWith('.csv')) {
    return 'Please select a CSV file';
  }
  if (file.size > MAX_FILE_SIZE) {
    return 'File size must be less than 10MB';
  }
  return null;
};
```

We'll keep this simple validation for now. Image support will be added in a later workshop step.

## Step 13.4: Enhanced Import Process with AI Processing Phases

*Update the import function to handle different processing phases and set up for enhancement.*

Replace the existing `handleImport` function:

```tsx
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

    const result = await transactionsApi.importTransactions({
      formData,
      onUploadProgress: (progressEvent) => {
        const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
        setUploadProgress(progress);

        // Update phase based on progress for CSV processing
        if (progress < 20) {
          setCurrentPhase('uploading');
        } else if (progress < 60) {
          setCurrentPhase('detecting');
        } else if (progress < 85) {
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

    showSuccess(`Successfully imported ${result.importedCount} transactions with AI description enhancements ready for review`);
  } catch (error) {
    console.error('Import error:', error);

    // Extract error message from the response
    let errorMessage = 'Failed to import the CSV file';
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
```

## Step 13.5: Update Helper Functions

*Add the updated helper functions for the multi-step workflow.*

Replace the existing `handleClearFile` function and add a retry function:

```tsx
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

const handleRetryImport = useCallback(() => {
  handleImport();
}, [handleImport]);
```

## Step 13.6: Build Step Indicator Component

*Create a visual progress indicator showing the current step in the process.*

Add this step indicator at the top of the main return statement:

```tsx
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
    {/* Rest of the component... */}
  </div>
);
```

## Step 13.7: Enhanced Upload Step with Confidence Selection

*Update the upload step to include AI confidence threshold selection.*

Replace the existing upload form section with:

```tsx
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
        accept=".csv"
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
              <span className="text-green-600 text-lg">📊</span>
            </div>
            <div>
              <p className="text-sm font-semibold text-gray-900">{selectedFile.name}</p>
              <p className="text-xs text-gray-600">CSV Bank Statement • {formatFileSize(selectedFile.size)}</p>
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
                    {currentPhase === 'uploading' ? 'Uploading file...' :
                      currentPhase === 'detecting' ? 'Detecting CSV structure...' :
                        currentPhase === 'parsing' ? 'Parsing transactions...' :
                          currentPhase === 'enhancing' ? 'Enhancing with AI...' :
                            'Finalizing...'}
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
            {currentPhase === 'uploading' ? 'Uploading file...' :
              currentPhase === 'detecting' ? 'Processing CSV...' :
                currentPhase === 'parsing' ? 'Parsing transactions...' :
                  currentPhase === 'enhancing' ? 'Enhancing with AI...' :
                    'Finalizing...'}
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
```

## Step 13.8: Test the Multi-Step Upload Process

*Test your enhanced upload workflow before moving to the enhancement preview.*

1. **Start your development servers**:
   ```bash
   # Backend
   cd src/BudgetTracker.Api/
   dotnet run

   # Frontend
   cd src/BudgetTracker.Web/
   npm run dev
   ```

2. **Test the upload workflow**:
   - **Step Indicator**: Verify the 3-step progress indicator appears
   - **File Selection**: Drag & drop or browse for `samples/generic-bank-to-enhance-sample.csv`
   - **Confidence Selection**: Try different confidence thresholds (30%, 50%, 70%)
   - **Account Input**: Enter "Checking Account"
   - **Upload Process**: Watch the sophisticated progress phases
   - **Success State**: Confirm it moves to "imported" step (ready for enhancement preview)

**You should see:**
- 🎯 **Professional UI**: Step indicators, enhanced styling, smooth transitions
- 📊 **Processing Phases**: Detailed progress through uploading → detecting → parsing → enhancing
- ⚙️ **AI Configuration**: Confidence threshold selection before import
- 🔄 **State Management**: Proper transitions between upload states
- 🚨 **Error Handling**: Comprehensive error handling with user feedback

---

## Summary ✅

You've successfully transformed the basic upload into a sophisticated multi-step workflow:

✅ **Multi-Step Foundation**: Upload → Import → Complete workflow with visual indicators
✅ **Enhanced Upload UI**: Professional drag-and-drop with confidence selection
✅ **Processing Phases**: Real-time feedback during CSV detection and parsing
✅ **State Management**: Complex state transitions and error handling
✅ **Professional Components**: Progress indicators and enhanced user interface
✅ **AI Configuration**: Pre-import confidence threshold selection

**Key Features Implemented:**
- **Step Indicator**: Visual progress through the 3-step workflow
- **Enhanced Upload Area**: Improved styling with feature badges
- **Confidence Selection**: Pre-configured AI enhancement thresholds
- **Processing Feedback**: Multi-phase progress with detailed descriptions
- **Error Recovery**: Comprehensive error handling with user feedback

**Technical Achievements:**
- Complex state management for multi-step workflows
- Professional progress indication with phase-specific feedback
- Enhanced user experience with smooth transitions
- Foundation prepared for AI enhancement preview interface

**Next Step**: Move to [014-react-enhancement-preview.md](014-react-enhancement-preview.md) to build the AI enhancement preview interface where users can review and decide on AI improvements before applying them.

The upload process now stops at the "imported" step, ready for the enhancement preview workflow in the next step!