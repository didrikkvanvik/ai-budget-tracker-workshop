# Workshop Step 021: Multimodal Image Import

## Mission 🎯

In this step, you'll add multimodal AI capabilities to your budget tracker, allowing users to upload images of bank statements and automatically extract transaction data using GPT-4 Vision. The AI will analyze statement images and convert them into structured transaction data, making it easier for users to import their financial data.

**Your goal**: Implement image processing functionality that can extract transactions from uploaded bank statement images using Azure OpenAI's vision capabilities.

**Learning Objectives**:
- Implementing multimodal AI with GPT-4 Vision for document processing
- Creating image processing workflows for financial data extraction
- Integrating image uploads with existing CSV import infrastructure
- Building reliable data extraction from unstructured image sources
- Handling confidence scoring and validation for AI-extracted data

---

## Prerequisites

Before starting, ensure you completed:
- [015-ai-transaction-categorization.md](015-ai-transaction-categorization.md) - AI categorization system

---

## Branches

**Starting branch:** `015-categorise-ai`
**Solution branch:** `021-image-import`

---

## Step 21.1: Create Image Import Interface

*Define the interface for processing bank statement images.*

The image import functionality needs a dedicated interface to handle the processing of uploaded images. This interface will be responsible for converting image streams into structured transaction data that can be integrated with the existing import workflow.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/IImageImporter.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface IImageImporter
{
    Task<(ImportResult Result, List<Transaction> Transactions)> ProcessImageAsync(
        Stream imageStream, string sourceFileName, string userId, string account);
}
```

## Step 21.2: Create JSON Extraction Extension

*Extract a shared utility for parsing JSON responses from AI code blocks.*

Since both the transaction enhancer and image importer need to parse JSON responses from AI that may be wrapped in code blocks, we'll create a reusable extension method to handle this common functionality.

Create `src/BudgetTracker.Api/Infrastructure/Extensions/JsonStringExtensions.cs`:

```csharp
using System.Text.RegularExpressions;

namespace BudgetTracker.Api.Infrastructure.Extensions;

public static class JsonStringExtensions
{
    public static string ExtractJsonFromCodeBlock(this string input)
    {
        if (!input.Contains("```json"))
            return input;
        
        var match = Regex.Match(input, @"```json\s*([\s\S]*?)\s*```");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        throw new FormatException("Could not extract JSON from the input string");
    }
}
```

## Step 21.3: Update Transaction Enhancer to Use Extension

*Refactor the existing transaction enhancer to use the shared JSON extraction utility.*

The `TransactionEnhancer` currently has its own `ExtractJsonFromCodeBlock` method. We need to update it to use the shared extension method and add the proper using statement.

Update `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Extensions; // Add this using statement
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public class TransactionEnhancer : ITransactionEnhancer
{
    // ... existing constructor and other methods ...

    private List<EnhancedTransactionDescription> ParseEnhancedDescriptions(string content,
        List<string> originalDescriptions)
    {
        try
        {
            var enhancedDescriptions = JsonSerializer.Deserialize<List<EnhancedTransactionDescription>>(
                content.ExtractJsonFromCodeBlock(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (enhancedDescriptions?.Count == originalDescriptions.Count)
            {
                return enhancedDescriptions;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON: {Content}", content);
        }

        _logger.LogWarning("AI response format was invalid, returning original descriptions");
        return originalDescriptions.Select(d => new EnhancedTransactionDescription
        {
            OriginalDescription = d,
            EnhancedDescription = d,
            ConfidenceScore = 0.0
        }).ToList();
    }

    // Remove the old ExtractJsonFromCodeBlock method - it's now an extension method
}
```

## Step 21.4: Implement Image Processing Service

*Create the core image processing service using Azure OpenAI Vision capabilities.*

The `ImageImporter` service will handle the complete workflow of processing bank statement images: converting images to base64, sending them to GPT-4 Vision for analysis, and parsing the AI response into structured transaction data.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/ImageImporter.cs`:

```csharp
using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;
using BudgetTracker.Api.Infrastructure.Extensions; // Add this using statement
using OpenAI.Chat;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class ImageImporter : IImageImporter
{
    private readonly IAzureChatService _chatService;
    private readonly ILogger<ImageImporter> _logger;

    public ImageImporter(
        IAzureChatService chatService,
        ILogger<ImageImporter> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<(ImportResult Result, List<Transaction> Transactions)> ProcessImageAsync(
        Stream imageStream, string sourceFileName, string userId, string account)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            // Convert image to base64
            var imageBytes = await ReadImageBytesAsync(imageStream);
            var base64Image = Convert.ToBase64String(imageBytes);

            _logger.LogInformation("Processing bank statement image {FileName} ({Size} bytes)",
                sourceFileName, imageBytes.Length);

            // Process image with GPT-4 Vision
            var extractedData = await ExtractTransactionsFromImageAsync(base64Image);

            // Parse and validate results
            var (parseResult, parsedTransactions) = ParseExtractionResults(extractedData, sourceFileName, userId, account);

            // Merge results
            result.TotalRows = parseResult.TotalRows;
            result.ImportedCount = parseResult.ImportedCount;
            result.FailedCount = parseResult.FailedCount;
            result.Errors.AddRange(parseResult.Errors);

            return (result, parsedTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {FileName}", sourceFileName);
            result.Errors.Add($"Image processing error: {ex.Message}. Please ensure the image shows a clear bank statement.");
            return (result, transactions);
        }
    }

    private async Task<byte[]> ReadImageBytesAsync(Stream imageStream)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private async Task<string> ExtractTransactionsFromImageAsync(string base64Image)
    {
        var systemPrompt = CreateTransactionExtractionPrompt();
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart("Extract all transactions from this bank statement image:"),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(Convert.FromBase64String(base64Image)), "image/png")
            )
        };

        return await _chatService.CompleteChatAsync(messages);
    }

    private string CreateTransactionExtractionPrompt()
    {
        return """
            You are a financial data extraction specialist. Extract transaction data from bank statement images.

            Return a JSON object with this exact structure:
            {
              "confidence_score": 0.95,
              "transactions": [
                {
                  "date": "2024-01-15",
                  "description": "STARBUCKS COFFEE #1234",
                  "amount": -4.50,
                  "balance": 1234.56,
                  "category": null
                }
              ]
            }

            Guidelines:
            - Extract ALL visible transactions from the statement
            - Use negative amounts for debits/expenses, positive for credits/income
            - Include running balance if visible
            - Date format: YYYY-MM-DD
            - Leave category as null (will be enhanced later)
            - Provide confidence score (0.0-1.0) based on image clarity and data completeness
            - If no transactions found, return empty transactions array with confidence explanation
            """;
    }

    private (ImportResult Result, List<Transaction> Transactions) ParseExtractionResults(
        string extractedData, string sourceFileName, string userId, string account)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            var jsonDocument = JsonDocument.Parse(extractedData.ExtractJsonFromCodeBlock());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("confidence_score", out var confidenceElement))
            {
                var confidence = confidenceElement.GetDouble();
                _logger.LogInformation("Image extraction confidence: {Confidence}", confidence);

                if (confidence < 0.7)
                {
                    result.Errors.Add($"Low confidence extraction ({confidence:P0}). Please verify the results carefully.");
                }
            }

            if (root.TryGetProperty("transactions", out var transactionsElement))
            {
                foreach (var transactionElement in transactionsElement.EnumerateArray())
                {
                    try
                    {
                        var transaction = ParseTransactionFromJson(transactionElement, userId, account);
                        if (transaction != null)
                        {
                            transactions.Add(transaction);
                            result.ImportedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Failed to parse transaction: {ex.Message}");
                    }
                }
            }

            result.TotalRows = result.ImportedCount + result.FailedCount;
            return (result, transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse extraction results");
            result.Errors.Add($"Failed to parse AI response: {ex.Message}");
            return (result, transactions);
        }
    }

    private Transaction? ParseTransactionFromJson(JsonElement transactionElement, string userId, string account)
    {
        if (!transactionElement.TryGetProperty("date", out var dateElement) ||
            !transactionElement.TryGetProperty("description", out var descriptionElement) ||
            !transactionElement.TryGetProperty("amount", out var amountElement))
        {
            return null;
        }

        if (!DateTime.TryParse(dateElement.GetString(), out var date))
        {
            throw new ArgumentException("Invalid date format");
        }

        var description = descriptionElement.GetString();
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required");
        }

        if (!amountElement.TryGetDecimal(out var amount))
        {
            throw new ArgumentException("Invalid amount format");
        }

        decimal? balance = null;
        if (transactionElement.TryGetProperty("balance", out var balanceElement) &&
            balanceElement.TryGetDecimal(out var balanceValue))
        {
            balance = balanceValue;
        }

        return new Transaction
        {
            Id = Guid.NewGuid(),
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Description = description,
            Amount = amount,
            Balance = balance,
            Category = null, // Will be enhanced later
            UserId = userId,
            Account = account,
            ImportedAt = DateTime.UtcNow
        };
    }
}
```

## Step 21.5: Register Image Import Service

*Add the image import service to the dependency injection container.*

The `ImageImporter` service needs to be registered with the DI container so it can be injected into the import API endpoints. This registration should be added alongside the other import-related services.

Update `src/BudgetTracker.Api/Program.cs` to include the image import service registration:

```csharp
// Add import services
builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<IImageImporter, ImageImporter>(); // Add this line
```

## Step 21.6: Update Import API for Image Processing

*Modify the import API to handle both CSV and image files.*

The current import API only handles CSV files. We need to update it to detect the file type and route image files to the new image processing service while maintaining the existing CSV processing functionality.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs` to support image processing:

```csharp
// Update the ImportAsync method signature to include IImageImporter
private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
    IFormFile file, [FromForm] string account,
    CsvImporter csvImporter, IImageImporter imageImporter, BudgetTrackerContext context,
    ITransactionEnhancer enhancementService, ClaimsPrincipal claimsPrincipal,
    ICsvStructureDetector detectionService, IServiceProvider serviceProvider
)
{
    var validationResult = ValidateFileInput(file);
    if (validationResult != null)
    {
        return validationResult;
    }

    try
    {
        var userId = claimsPrincipal.GetUserId();
        await using var stream = file.OpenReadStream();

        var (importResult, transactions, detectionResult) = await ProcessFileAsync(
            stream, file.FileName, userId, account, csvImporter, imageImporter, detectionService);

        var importSessionHash = GenerateImportSessionHash(file.FileName, account);
        AssignImportSessionToTransactions(transactions, importSessionHash);

        await SaveTransactionsAsync(context, transactions);

        var enhancementResults = await ProcessEnhancementsAsync(
            enhancementService, transactions, account, userId, importSessionHash);

        var result = CreateImportResult(importResult, importSessionHash, enhancementResults, detectionResult);

        return TypedResults.Ok(result);
    }
    catch (Exception ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
}

// Add the ProcessFileAsync method to route files to appropriate processors
private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessFileAsync(
    Stream stream, string fileName, string userId, string account,
    CsvImporter csvImporter, IImageImporter imageImporter, ICsvStructureDetector detectionService)
{
    var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
    return fileExtension switch
    {
        ".csv" => await ProcessCsvFileAsync(stream, fileName, userId, account, csvImporter, detectionService),
        ".png" or ".jpg" or ".jpeg" => await ProcessImageFileAsync(stream, fileName, userId, account, imageImporter),
        _ => throw new InvalidOperationException("Unsupported file type")
    };
}

// Add the ProcessImageFileAsync method
private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessImageFileAsync(
    Stream stream, string fileName, string userId, string account,
    IImageImporter imageImporter)
{
    var (importResult, transactions) = await imageImporter.ProcessImageAsync(stream, fileName, userId, account);

    return (importResult, transactions, null); // Images don't have CSV detection result
}

// Update ValidateFileInput to accept image files
private static BadRequest<string>? ValidateFileInput(IFormFile file)
{
    if (file == null || file.Length == 0)
    {
        return TypedResults.BadRequest("Please select a valid file.");
    }

    const int maxFileSize = 10 * 1024 * 1024; // 10MB
    if (file.Length > maxFileSize)
    {
        return TypedResults.BadRequest("File size must be less than 10MB.");
    }

    var allowedExtensions = new[] { ".csv", ".png", ".jpg", ".jpeg" };
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(fileExtension))
    {
        return TypedResults.BadRequest("Only CSV files and images (PNG, JPG, JPEG) are supported.");
    }

    return null;
}
```

## Step 21.7: Update Frontend for Image Upload Support

*Modify the frontend file upload component to accept and display image files.*

The current frontend only accepts CSV files. We need to update the `FileUpload` component to accept image files and provide appropriate visual feedback to users when they upload bank statement images.

Update `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`:

```tsx
// Update the file input to accept images
<input
  ref={fileInputRef}
  type="file"
  accept=".csv,.png,.jpg,.jpeg"
  onChange={handleFileInputChange}
  className="hidden"
/>

// Update the validateFile function
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

// Update the file type detection functions
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

// Update the upload progress phases
const [currentPhase, setCurrentPhase] = useState<'uploading' | 'detecting' | 'parsing' | 'extracting' | 'enhancing' | 'complete'>('uploading');

// Update the phase progression logic in handleImport (keep existing method name)
const handleImport = async () => {
  if (!selectedFile || !account.trim()) return;

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
  } catch (error: any) {
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
  }
};
```

## Step 21.8: Update Detection Progress for Images

*Modify the progress indicator to show appropriate phases for image processing.*

The detection progress indicator needs to display different phases when processing images versus CSV files. Image processing involves extraction rather than structure detection and parsing.

Update `src/BudgetTracker.Web/src/features/transactions/components/DetectionProgressIndicator.tsx`:

```tsx
// Update the getPhaseDescription function
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

// Update the phase icon mapping
const getPhaseIcon = (phase: string, fileName: string): string => {
  const isImage = fileName.toLowerCase().match(/\.(png|jpg|jpeg)$/);

  switch (phase) {
    case 'uploading':
      return '⬆️';
    case 'detecting':
      return isImage ? '🔍' : '🔍';
    case 'parsing':
      return '📊';
    case 'extracting':
      return '🖼️';
    case 'enhancing':
      return '✨';
    case 'complete':
      return '✅';
    default:
      return '⚙️';
  }
};
```

## Step 21.9: Test Image Import Functionality

*Test the complete image import workflow with sample bank statement images.*

### 21.9.1: Test Image Upload

Test the image import functionality:

```http
### Test image import with a bank statement screenshot
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="bank-statement.png"
Content-Type: image/png

[Binary image data would go here]
--WebAppBoundary--
```

### 21.9.2: Verify Extraction Results

After importing, verify that:

**Expected Behavior:**
- ✅ **Image Processing**: GPT-4 Vision successfully analyzes the bank statement image
- ✅ **Data Extraction**: Transactions are extracted with proper dates, descriptions, and amounts
- ✅ **Error Handling**: Low confidence extractions display appropriate warnings
- ✅ **Enhancement Integration**: Extracted transactions flow through the existing enhancement pipeline
- ✅ **UI Integration**: Image files display with appropriate icons and labels

### 21.9.3: Test Error Scenarios

Test various error scenarios:

**1. Unsupported File Type**
```http
POST http://localhost:5295/api/transactions/import
# Upload a .txt file - should return error about unsupported file type
```

**2. Large File Size**
```http
POST http://localhost:5295/api/transactions/import
# Upload a file larger than 10MB - should return size limit error
```

**3. Poor Quality Image**
```http
POST http://localhost:5295/api/transactions/import
# Upload a blurry or unclear image - should return low confidence warning
```

---

## Summary ✅

You've successfully implemented multimodal image import functionality for your budget tracker:

✅ **multimodal AI Integration**: GPT-4 Vision processes bank statement images and extracts structured data
✅ **Intelligent Extraction**: AI analyzes images and identifies transactions with confidence scoring
✅ **Seamless Integration**: Image processing integrates with existing CSV import workflow
✅ **Enhanced User Experience**: Users can upload images directly without manual data entry
✅ **Error Handling**: Comprehensive validation and error reporting for image processing failures
✅ **Progress Indication**: Visual feedback shows different phases for image vs CSV processing

**Key Features Implemented**:
- **Image Processing Service**: Core service that converts bank statement images to transaction data
- **Vision AI Integration**: Uses GPT-4 Vision to analyze and extract data from uploaded images
- **Unified Import API**: Single endpoint handles both CSV and image file uploads seamlessly
- **Confidence Scoring**: AI provides confidence scores and warnings for uncertain extractions
- **Enhanced Frontend**: Upload interface supports images with appropriate visual indicators
- **Progress Tracking**: Different processing phases for image extraction vs CSV parsing

**Technical Achievements**:
- **multimodal Prompting**: Sophisticated system prompts for accurate financial data extraction
- **JSON Response Parsing**: Robust parsing of AI-generated transaction data with validation
- **Shared Utility Extraction**: Reusable extension method for parsing JSON from AI code blocks
- **Stream Processing**: Efficient handling of image file streams and base64 conversion
- **Error Recovery**: Graceful handling of AI extraction failures and invalid responses
- **Code Reusability**: Refactored common JSON extraction logic into shared extension methods
- **Integration Architecture**: Image import seamlessly integrates with existing enhancement pipeline

**What Users Get**:
- **Effortless Import**: Take a photo of a bank statement and import transactions instantly
- **Time Savings**: No need to manually type transaction data from paper statements
- **AI Accuracy**: Advanced vision AI ensures accurate data extraction from various statement formats
- **Familiar Workflow**: Image imports follow the same enhancement and review process as CSV imports
- **Quality Assurance**: Confidence scores help users identify when manual verification is needed

The system now supports both traditional CSV imports and modern image-based imports, giving users maximum flexibility in how they add transaction data to their budget tracker! 🎉