using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;
using BudgetTracker.Api.Infrastructure.Extensions;
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