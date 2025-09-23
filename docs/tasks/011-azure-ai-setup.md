# Workshop Step 006: Azure AI Setup

## Mission 🎯

In this step, you'll set up Azure OpenAI resources through the Azure portal and configure your development environment. This foundation will enable AI-powered transaction enhancement in the next steps.

**Your goal**: Create and configure an Azure OpenAI resource with proper deployment and security settings for the workshop.

**Learning Objectives**:
- Azure portal navigation and resource creation
- Azure OpenAI service configuration
- Security best practices for AI services
- Environment variable management

---

## Prerequisites

Before starting, ensure you have:
- **Azure Account**
- Completed previous workshop steps (001-005)

---

## Branches

**Starting branch:** `005-react-transactions-import`

---

## Step 6.1: Create Azure OpenAI Resource

*Navigate to the Azure portal and create a new OpenAI resource.*

### 6.1.1: Access Azure Portal

1. **Go to Azure Portal**: https://portal.azure.com
2. **Sign in** with your Azure account credentials
3. **Navigate to Create a resource** (+ icon in the top-left)

### 6.1.2: Search for OpenAI

1. **Search for "OpenAI"** in the marketplace
2. **Select "Azure OpenAI"** from the results
3. **Click "Create"** to start the setup process

### 6.1.3: Configure Basic Settings

Fill in the resource creation form:

**Project Details:**
- **Subscription**: Select your Azure subscription
- **Resource Group**: Create new or use existing (e.g., "budget-tracker-rg")

**Instance Details:**
- **Region**: Choose a region that supports OpenAI (e.g., West Europe)
- **Name**: Enter a unique name (e.g., "budget-tracker-ai-[yourname]")
- **Pricing Tier**: Select "Standard S0" (pay-as-you-go)

**Click "Next: Network"** to continue.

### 6.1.4: Network Configuration

For workshop purposes, use the default settings:
- **Network Access**: "All networks, including the internet, can access this resource"

**Note**: In production, you'd want to restrict network access.

**Click "Next: Tags"** to continue.

### 6.1.5: Add Tags (Optional)

Add tags for organization:
- **Environment**: "development"
- **Project**: "budget-tracker-workshop"
- **Owner**: Your name or team

**Click "Next: Review + create"** to continue.

### 6.1.6: Review and Create

1. **Review your configuration**
2. **Click "Create"** to deploy the resource
3. **Wait for deployment** (usually takes 2-3 minutes)
4. **Click "Go to resource"** when deployment completes

---

## Step 6.2: Deploy AI Model

*Create a model deployment that your application will use.*

### 6.2.1: Navigate to Model Deployments
1. In your OpenAI resource, follow the link to the Azure AI Foundry Portal
2. Click **"Deployments"** in the left sidebar
3. Click **"Deploy model"**
4. Click **"Deploy base model"**

### 6.2.2: Configure Model Deployment

**Deployment Settings:**
- **Model**: Select "gpt-4o-mini" (recommended)
- **Model version**: Use the default latest version
- **Deployment name**: Enter "gpt-4o-mini" (remember this name!)
- **Content filter**: Use default settings
- **Deployment type**: Standard
- **Tokens per minute rate limit**: Use default settings

**Click "Deploy"** to deploy the model.

### 6.2.3: Verify Deployment

1. **Wait for deployment** to complete (1-2 minutes)
2. **Verify status** shows as "Succeeded"
3. **Note the deployment name** - you'll need this in your code

---

## Step 6.3: Get API Credentials

*Collect the endpoint and API keys needed for your application.*

### 6.3.1: Get Endpoint URL

1. In your Azure OpenAI home page
2. **Copy the "Azure OpenAI endpoint" URL** (looks like: `https://your-resource.openai.azure.com/`)
3. **Save this URL** - you'll need it for configuration

### 6.3.2: Get API Keys

1. **Copy "API Key 1"** (click the copy button)
2. **Save this key securely** - treat it like a password

**Security Note**: Never commit API keys to version control!

---

## Step 6.4: Test Azure OpenAI Access

*Verify your setup works using the Azure OpenAI Studio.*

### 6.4.1: Open Azure AI Foundry

1. In your OpenAI resource, click **"Go to Azure AI Foundry Portal"**
2. This opens a new tab with the portal

### 6.4.2: Test Chat Completion

1. Navigate to **"Chat"** in the left sidebar
2. **Select your deployment** from the dropdown
3. **Test with a simple prompt**:
   ```
   Transform this bank transaction: "AMZN MKTP US*123456789" into a readable description.
   ```
4. **Verify you get a response** like _"The bank transaction "AMZN MKTP US*123456789" can be described as a purchase made through Amazon Marketplace in the United States, with the transaction possibly linked to a specific order or account identified by the number 123456789."_

If this works, your Azure OpenAI setup is complete!

---

## Step 6.5: Configure Development Environment

*Set up your local environment with the Azure credentials using .NET User Secrets for security.*

### 6.5.1: Initialize User Secrets

*User Secrets keeps sensitive data out of your codebase and git repository.*

```bash
cd src/BudgetTracker.Api/
dotnet user-secrets init
```

This creates a unique secrets ID in your project file and prepares the secrets storage.

### 6.5.2: Set Azure OpenAI Secrets

*Add your Azure OpenAI credentials to the secure user secrets store.*

```bash
# Set your Azure OpenAI endpoint
dotnet user-secrets set "AzureAI:Endpoint" "https://your-resource.openai.azure.com/"

# Set your API key
dotnet user-secrets set "AzureAI:ApiKey" "your-api-key-here"

# Set your deployment name
dotnet user-secrets set "AzureAI:DeploymentName" "gpt-4o-mini"
```

**Replace with your actual values:**
- `your-resource`: Your actual Azure resource name
- `your-api-key-here`: The API key you copied from Azure portal
- `gpt-4o-mini`: Your actual deployment name

### 6.5.3: Verify User Secrets

*List your secrets to verify they were set correctly (values are hidden for security).*

```bash
dotnet user-secrets list
```

You should see output like:
```
AzureAI:Endpoint = https://your-resource.openai.azure.com/
AzureAI:ApiKey = [Hidden]
AzureAI:DeploymentName = gpt-4o-mini
```

### 6.5.4: Update appsettings Structure

*Add the AzureAI configuration section to your appsettings.json (without sensitive values).*

Update `src/BudgetTracker.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=budgettracker;Username=budgetuser;Password=budgetpass123"
  },
  "AzureAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Note**: The empty values will be overridden by user secrets during development. This shows the expected configuration structure without exposing secrets.

---

## Troubleshooting 🔧

### Common Issues

**"Access Denied" when creating OpenAI resource:**
- Ensure you have Azure OpenAI access approval
- Try a different region (East US, West Europe typically work)
- Check your Azure subscription has sufficient permissions

**"Deployment failed" for model:**
- Verify the model is available in your chosen region
- Try reducing the token rate limit
- Ensure you have sufficient quota

**User Secrets not working:**
- Ensure you ran `dotnet user-secrets init` from the correct project directory
- Verify secrets are set with `dotnet user-secrets list`
- Check that the secrets key names match exactly (case-sensitive)

**API key issues:**
- Regenerate keys in Azure portal if needed
- Ensure no extra spaces in copied keys
- Try Key 2 if Key 1 doesn't work

---

## Summary ✅

You've successfully set up:

✅ **Azure OpenAI Resource**: Created and configured in Azure portal
✅ **Model Deployment**: GPT-4o (or GPT-3.5-turbo) deployed and ready
✅ **API Credentials**: Endpoint and API key collected securely
✅ **Development Environment**: Local configuration with .NET User Secrets
✅ **Security Setup**: Credentials stored securely outside of codebase

**Security Best Practices Implemented**:
- API keys stored in .NET User Secrets, never in code
- Configuration structure defined without sensitive values
- Secrets isolated per developer environment

**Next Step**: Move to [012-ai-transaction-enhancement-backend.md](012-ai-transaction-enhancement-backend.md) to implement the AI service that will use your Azure OpenAI resource.

---

## Additional Resources

- **Azure AI Foundry documentation**: https://learn.microsoft.com/en-gb/azure/ai-foundry/
- **Azure AI Foundry**: https://ai.azure.com/
- **Pricing Calculator**: https://azure.microsoft.com/pricing/calculator/