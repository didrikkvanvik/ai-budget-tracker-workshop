# Workshop Step 015: AI Transaction Categorization

## Mission 🎯

In this step, you'll enhance your existing AI transaction enhancement system by adding intelligent category suggestions. The AI will analyze transaction descriptions and automatically suggest appropriate spending categories like "Groceries", "Entertainment", "Utilities", etc.

**Your goal**: Add category suggestion functionality to the AI enhancement service and ensure suggested categories are properly applied to imported transactions.

**Learning Objectives**:
- Adding category suggestions to the AI enhancement workflow
- Configuring AI prompts for accurate categorization
- Integrating category suggestions into the import process
- Updating the user interface to display category suggestions
- Applying suggested categories to transaction records

---

## Prerequisites

Before starting, ensure you completed:
- [014-react-enhancement-preview.md](014-react-enhancement-preview.md) - Description-only enhancement workflow

---

## Branches

**Starting branch:** `014-enhancement-ai`
**Solution branch:** `015-categorise-ai`

---

## Step 15.1: Add Category Property to Enhancement Interface

*Extend the enhancement interface to include category suggestions alongside description improvements.*

The `EnhancedTransactionDescription` class currently only handles description enhancement. We need to add a property for category suggestions so the AI can provide both improved descriptions and suggested categories in a single response.

Update `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/ITransactionEnhancer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
    Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string? currentImportSessionHash = null);
}

public class EnhancedTransactionDescription
{
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; } // Add category suggestions
    public double ConfidenceScore { get; set; }
}
```

## Step 15.2: Update AI System Prompt for Categorization

*Modify the AI system prompt to request both description enhancement and category suggestions.*

The current AI prompt only asks for description enhancement. We need to update it to also request category suggestions. The AI will analyze each transaction description and suggest an appropriate spending category based on the merchant type and transaction amount.

Replace the `CreateEnhancedSystemPrompt` method in `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`:

```csharp
private static string CreateEnhancedSystemPrompt()
{
    return """
        You are a transaction enhancement and categorization assistant. Your job is to clean up messy bank transaction descriptions and suggest appropriate spending categories.

        Guidelines:
        1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
        2. Remove unnecessary reference numbers, codes, and technical identifiers
        3. Identify the actual merchant or service provider
        4. Suggest appropriate spending categories based on the merchant type and transaction purpose
        5. Maintain accuracy - don't invent information not present in the original

        Examples:
        - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase" (Category: Shopping)
        - "STARBUCKS COFFEE #1234" → "Starbucks Coffee" (Category: Food & Drink)
        - "SHELL OIL #4567" → "Shell Gas Station" (Category: Gas & Fuel)
        - "DD VODAFONE PORTU 222111000" → "Vodafone Portugal - Direct Debit" (Category: Utilities)
        - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase" (Category: Shopping)
        - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva" (Category: Transfer)

        Common categories to use:
        - Shopping, Groceries, Food & Drink, Entertainment, Gas & Fuel
        - Utilities, Transportation, Healthcare, Transfer, Cash & ATM
        - Technology, Subscriptions, Travel, Education, Other

        Respond with a JSON array where each object has:
        - "originalDescription": the input description
        - "enhancedDescription": the cleaned description
        - "suggestedCategory": appropriate category from the list above
        - "confidenceScore": number between 0-1 indicating confidence in both enhancement and categorization

        Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification and category.
        """;
}
```

## Step 15.3: Apply Categories in the Import Process

*Modify the import logic to apply AI-suggested categories to transaction records.*

Currently, the import process only applies enhanced descriptions to transactions. We need to update it to also apply the suggested categories when they're provided by the AI. This ensures that imported transactions automatically get categorized based on the AI's analysis.

Update the import logic in `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`:

```csharp
// Apply enhanced descriptions back to transactions
foreach (var transaction in transactions)
{
    var enhancement = enhancedDescriptions.FirstOrDefault(e =>
        e.OriginalDescription == transaction.Description);

    if (enhancement != null)
    {
        transaction.Description = enhancement.EnhancedDescription;
        if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
        {
            transaction.Category = enhancement.SuggestedCategory;
        }
    }

    // Set session hash for tracking
    transaction.ImportSessionHash = sessionHash;
}
```

## Step 15.4: Update Enhancement Result Types

*Add category fields to the import result types so the UI can display category suggestions.*

The `TransactionEnhancementResult` class needs to include category information so that the frontend can display both the enhanced description and the suggested category to users during the import preview process.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportResult.cs`:

```csharp
public class TransactionEnhancementResult
{
    public Guid TransactionId { get; set; }
    public string ImportSessionHash { get; set; } = string.Empty;
    public int TransactionIndex { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; } // Add category suggestions
    public double ConfidenceScore { get; set; }
}
```

And update the enhancement processing method:

```csharp
private static async Task<List<TransactionEnhancementResult>> ProcessEnhancementsAsync(
    ITransactionEnhancer enhancer, List<Transaction> transactions,
    string account, string userId, string sessionHash)
{
    var descriptions = transactions.Select(t => t.Description).Distinct().ToList();
    var enhancements = await enhancer.EnhanceDescriptionsAsync(descriptions, account, userId, sessionHash);

    // Create enhancement results for preview
    return enhancements.Select((enhancement, index) => new TransactionEnhancementResult
    {
        TransactionId = transactions[index].Id,
        ImportSessionHash = sessionHash,
        TransactionIndex = index,
        OriginalDescription = enhancement.OriginalDescription,
        EnhancedDescription = enhancement.EnhancedDescription,
        SuggestedCategory = enhancement.SuggestedCategory,
        ConfidenceScore = enhancement.ConfidenceScore
    }).ToList();
}
```

## Step 15.5: Update Frontend Types

*Add category support to the React frontend TypeScript interfaces.*

The frontend types need to be updated to handle category suggestions. This ensures type safety when displaying category information in the user interface during the import preview process.

Update `src/BudgetTracker.Web/src/features/transactions/types.ts`:

```tsx
export interface TransactionEnhancement {
  originalDescription: string;
  enhancedDescription: string;
  suggestedCategory?: string; // Add category suggestions
  confidenceScore: number;
}

export interface ImportResult {
  importedCount: number;
  failedCount: number;
  errors: string[];
  importSessionHash: string;
  enhancements: TransactionEnhancement[];
}
```

## Step 15.6: Update Enhancement Preview UI

*Add category display to the enhancement preview interface.*

The import preview interface needs to show both enhanced descriptions and suggested categories to users. This allows users to see exactly what changes the AI will make to their transactions before applying them.

Update the enhancement preview in `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`:

```tsx
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
```

## Step 15.7: Update Enhancement Application Logic

*Ensure the enhancement endpoint applies both descriptions and categories to transactions.*

The enhancement endpoint needs to be updated to apply both the enhanced descriptions and suggested categories when users choose to apply the AI improvements. This completes the categorization workflow.

Update the enhancement endpoint logic in the enhance import method:

```csharp
// Apply enhancement if confidence is high enough
if (!(enhancement.ConfidenceScore >= request.MinConfidenceScore)) continue;

// Find the transaction to enhance
var transaction = transactions.FirstOrDefault(t => t.Id == enhancement.TransactionId);
if (transaction == null) continue;

// Apply the enhancements
transaction.Description = enhancement.EnhancedDescription;

if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
{
    transaction.Category = enhancement.SuggestedCategory;
}

enhancedCount++;
```

## Step 15.8: Test AI Categorization

*Test the categorization system to ensure it's working correctly.*

### 15.8.1: Test with Sample Data

Test the complete categorization workflow using the provided sample file:

```http
### Test AI categorization with sample transactions
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="generic-bank-to-enhance-sample.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
2025-01-15,AMZN,-45.67,1250.33
2025-01-16,STARBUCKS COFFEE #1234,-5.89,1244.44
2025-01-17,TRF F/ John,2500.00,3744.44
2025-01-18,NFLX Subscription,-15.99,3728.45
2025-01-19,DD VODAFONE PORTU 222111000,-52.30,3676.15
2025-01-20,Grocery Store,-89.45,3586.70
--WebAppBoundary--
```

### 15.8.2: Verify Enhancement Preview

After importing, check that the enhancement preview shows both enhanced descriptions and suggested categories:

**Expected Results:**
- ✅ **"AMZN"** → **"Amazon"** (Category: Shopping)
- ✅ **"STARBUCKS COFFEE #1234"** → **"Starbucks Coffee"** (Category: Food & Drink)
- ✅ **"TRF F/ John"** → **"Transfer from John"** (Category: Transfer)
- ✅ **"NFLX Subscription"** → **"Netflix Subscription"** (Category: Entertainment)
- ✅ **"DD VODAFONE PORTU 222111000"** → **"Vodafone Portugal - Direct Debit"** (Category: Utilities)
- ✅ **"Grocery Store"** → **"Grocery Store"** (Category: Groceries)

### 15.8.3: Verify Category Application

Apply the enhancements and verify that the categories are properly saved to the database:

```http
### View imported transactions to verify categories
GET http://localhost:5295/api/transactions
X-API-Key: test-key-user1
```

You should see that each transaction now has both an enhanced description and an appropriate category.

---

## Summary ✅

You've successfully added AI categorization to your transaction enhancement system:

✅ **Category Integration**: Added category suggestions alongside description enhancement
✅ **AI Prompting**: Configured AI to suggest appropriate spending categories
✅ **Import Process**: Categories are automatically applied during transaction import
✅ **Enhanced UI**: Category suggestions displayed in the enhancement preview interface
✅ **User Control**: Users can review and approve category suggestions before applying
✅ **Data Flow**: Complete integration from AI suggestion to database storage

**Key Features Implemented**:
- **Dual Enhancement**: AI provides both description cleanup and category suggestions
- **Smart Categorization**: AI analyzes merchant patterns to suggest appropriate categories
- **Preview Interface**: Users see both enhanced descriptions and suggested categories
- **Automatic Application**: Suggested categories are saved to transaction records
- **Type Safety**: Full TypeScript interface support for category data

**Technical Achievements**:
- **Enhanced AI Prompt**: Updated system prompt to handle both description and category tasks
- **Data Model Updates**: Extended all relevant classes to support category suggestions
- **UI Integration**: Enhanced preview interface shows category information
- **Complete Workflow**: End-to-end integration from AI analysis to database storage
- **Error Handling**: Graceful fallbacks when categorization is uncertain

**What Users Get**:
- **Automatic Categorization**: Transactions get categorized without manual effort
- **Smart Suggestions**: AI analyzes merchant types and suggests relevant categories
- **Time Savings**: No need to manually categorize imported transactions
- **Consistent Categorization**: Similar merchants get consistent category assignments
- **Transparency**: Users can see and approve AI suggestions before applying

The system now provides intelligent transaction categorization that works seamlessly with the existing description enhancement, giving users both clean descriptions AND organized categories automatically! 🎉