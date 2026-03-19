# ⚡ ResumePulse — AI-Powered Resume Analyzer

> An end-to-end Azure-native portfolio project demonstrating event-driven architecture with AI resume analysis using Gemini, Blazor WASM, Azure Functions, Service Bus, Event Grid, Key Vault, and Blob Storage.

---

## 📌 Overview

**ResumePulse** is a cloud-native AI resume analyzer built as a portfolio/POC project. A user uploads their resume (PDF) along with a job description — the system uses **Google Gemini AI** to score the resume, extract matched/missing skills, and return a recommendation. Results are delivered both in-browser and via email notification.

This project showcases real-world Azure architecture patterns suited for AZ-204 / AZ-305 certification preparation.

---

## 🏗️ Architecture

```
User (Blazor WASM)
       │
       ▼
ASP.NET Core API  ──► Azure Blob Storage (resumes/)
       │
       ▼
Azure Service Bus Queue (resume-analysis-queue)
       │
       ▼
Azure Function (ServiceBusTrigger)
       │
       ├──► Google Gemini AI  (resume analysis)
       │
       ├──► Azure Blob Storage (reports/)  ← report.json saved here
       │
       └──► Azure Event Grid  ──► [Email Notification / Future Subscribers]
                    │
                    ▼
             Key Vault (all secrets centralized)
```

### Azure Services Used

| Service                   | Purpose                                                     |
| ------------------------- | ----------------------------------------------------------- |
| **Azure App Service**     | Hosts the ASP.NET Core Web API                              |
| **Azure Static Web Apps** | Hosts the Blazor WASM frontend                              |
| **Azure Blob Storage**    | Stores uploaded resumes and generated reports               |
| **Azure Service Bus**     | Decouples upload from analysis (queue-based messaging)      |
| **Azure Functions v4**    | Processes the analysis queue asynchronously                 |
| **Azure Event Grid**      | Publishes analysis-complete events for downstream consumers |
| **Azure Key Vault**       | Centralizes all secrets (connection strings, API keys)      |
| **Google Gemini AI**      | Performs resume-to-job-description analysis via LLM         |

---

## 🛠️ Tech Stack

- **Frontend:** Blazor WebAssembly (.NET 9)
- **Backend API:** ASP.NET Core Web API (.NET 9)
- **Serverless:** Azure Functions v4 (Isolated Worker, .NET 9)
- **AI:** Google Gemini (`gemini-2.0-flash` via `Google.GenAI` SDK)
- **Messaging:** Azure Service Bus (Queue)
- **Events:** Azure Event Grid (Custom Topic)
- **Storage:** Azure Blob Storage
- **Secrets:** Azure Key Vault + `DefaultAzureCredential`
- **Documentation:** Swagger / OpenAPI (Swashbuckle)

---

## 📁 Project Structure

```
ResumePulse/
├── ResumePulse.Api/                  # ASP.NET Core Web API
│   ├── Controllers/
│   │   └── ResumeController.cs       # Upload + report retrieval endpoints
│   ├── Models/
│   │   └── ResumeUploadRequest.cs    # Request/Response DTOs
│   ├── Services/
│   │   ├── BlobStorageService.cs     # Azure Blob upload/delete
│   │   └── ServiceBusService.cs      # Service Bus message sender
│   ├── Program.cs                    # App configuration + Key Vault integration
│   └── appsettings.*.json
│
├── ResumePulse.Client/               # Blazor WebAssembly Frontend
│   ├── Pages/
│   │   └── Home.razor                # Main upload + results UI
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Services/
│   │   └── ResumeAPIService.cs       # HTTP client + polling logic
│   └── Program.cs
│
└── ResumePulse.Functions/            # Azure Functions (Isolated Worker)
    ├── Functions/
    │   └── ResumeAnalysisFunction.cs # ServiceBusTrigger → Gemini → Blob → EventGrid
    ├── Models/
    │   └── ResumeAnalysisMessages.cs # Message + Report models
    ├── Services/
    │   └── GeminiService.cs          # Gemini AI integration with retry logic
    ├── Program.cs
    └── host.json
```

---

## 🔄 Request Flow

1. **User uploads** a PDF resume + job description via Blazor WASM UI
2. **API** (`POST /api/resume/upload`) stores the PDF in `resumes/` Blob container and sends a message to Service Bus queue — returns `jobId`
3. **Azure Function** is triggered by the Service Bus message:
   - Downloads the resume from Blob Storage
   - Sends resume text + job description to **Gemini AI**
   - Saves the structured JSON report to `reports/{jobId}/report.json`
   - Publishes a `ResumePulse.ResumeAnalysisCompleted` event to **Event Grid**
4. **Frontend polls** `GET /api/resume/report/{jobId}` every 5 seconds until the report is ready
5. **Report is displayed** in-browser with score, matched/missing skills, and recommendation badge

---

## 📊 AI Report Schema

```json
{
  "jobId": "guid",
  "applicantEmail": "user@example.com",
  "overallScore": 78,
  "scoreSummary": "Strong backend match. Missing cloud-native experience.",
  "matchedSkills": ["C#", ".NET", "REST APIs", "SQL"],
  "missingSkills": ["Kubernetes", "Terraform", "CI/CD"],
  "recommendation": "Good Match",
  "generatedAt": "2026-03-19T10:00:00Z"
}
```

**Recommendation values:** `Strong Match` · `Good Match` · `Partial Match` · `Not a Match`

---

## ⚙️ Configuration

### API — `appsettings.Development.json`

```json
{
  "KeyVault": {
    "Url": "https://<your-keyvault>.vault.azure.net/"
  },
  "AzureStorage": {
    "ResumesContainer": "resumes",
    "ReportsContainer": "reports"
  },
  "ServiceBus": {
    "QueueName": "resume-analysis-queue"
  }
}
```

### Key Vault Secrets Required

| Secret Name                  | Description                                   |
| ---------------------------- | --------------------------------------------- |
| `StorageConnectionString`    | Azure Storage Account connection string       |
| `ServiceBusConnectionString` | Azure Service Bus namespace connection string |

### Azure Functions — `local.settings.json` (not committed)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<storage-connection-string>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnectionString": "<servicebus-connection-string>",
    "StorageConnectionString": "<storage-connection-string>",
    "GeminiApiKey": "<your-gemini-api-key>",
    "EventGridTopicEndpoint": "https://<topic>.eventgrid.azure.net/api/events",
    "EventGridTopicKey": "<event-grid-key>",
    "KeyVault__Url": "https://<your-keyvault>.vault.azure.net/"
  }
}
```

---

## 📦 NuGet Packages

All packages are restored automatically via `dotnet restore`. The tables below list every package per project for reference.

### ResumePulse.Api

| Package                                             | Version | Purpose                                                        |
| --------------------------------------------------- | ------- | -------------------------------------------------------------- |
| `Azure.Extensions.AspNetCore.Configuration.Secrets` | 1.5.0   | Load secrets from Key Vault into `IConfiguration`              |
| `Azure.Identity`                                    | 1.19.0  | `DefaultAzureCredential` for Key Vault + managed identity auth |
| `Azure.Messaging.ServiceBus`                        | 7.20.1  | Send messages to the Service Bus queue                         |
| `Azure.Storage.Blobs`                               | 12.27.0 | Upload/download resumes and reports from Blob Storage          |
| `Microsoft.AspNetCore.Http.Features`                | 5.0.17  | HTTP features support (file upload streaming)                  |
| `Microsoft.AspNetCore.OpenApi`                      | 9.0.9   | OpenAPI document generation                                    |
| `Swashbuckle.AspNetCore`                            | 6.9.0   | Swagger UI for interactive API documentation                   |

```bash
cd ResumePulse.Api
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets --version 1.5.0
dotnet add package Azure.Identity --version 1.19.0
dotnet add package Azure.Messaging.ServiceBus --version 7.20.1
dotnet add package Azure.Storage.Blobs --version 12.27.0
dotnet add package Microsoft.AspNetCore.Http.Features --version 5.0.17
dotnet add package Microsoft.AspNetCore.OpenApi --version 9.0.9
dotnet add package Swashbuckle.AspNetCore --version 6.9.0
```

---

### ResumePulse.Client (Blazor WASM)

The Blazor WebAssembly frontend has **no additional NuGet packages** beyond the default `Microsoft.AspNetCore.Components.WebAssembly` framework included with the .NET 9 WASM SDK. All HTTP communication is done via the built-in `HttpClient`.

```bash
cd ResumePulse.Client
dotnet restore   # restores built-in WASM framework references only
```

---

### ResumePulse.Functions

| Package                                                       | Version | Purpose                                         |
| ------------------------------------------------------------- | ------- | ----------------------------------------------- |
| `Azure.Extensions.AspNetCore.Configuration.Secrets`           | 1.5.0   | Key Vault integration in Functions host         |
| `Azure.Identity`                                              | 1.19.0  | `DefaultAzureCredential` for managed identity   |
| `Azure.Messaging.EventGrid`                                   | 5.0.0   | Publish events to Event Grid custom topic       |
| `Azure.Messaging.ServiceBus`                                  | 7.20.1  | Service Bus client (used inside function logic) |
| `Azure.Storage.Blobs`                                         | 12.27.0 | Download resume PDF and upload report JSON      |
| `Google.GenAI`                                                | 1.3.0   | Google Gemini AI SDK for resume analysis        |
| `Microsoft.Azure.Functions.Worker`                            | 2.0.0   | Isolated worker host for Azure Functions v4     |
| `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus`      | 5.24.0  | `ServiceBusTrigger` binding support             |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | 2.0.0   | HTTP trigger support in isolated worker         |
| `Microsoft.Azure.Functions.Worker.Sdk`                        | 2.0.0   | Build SDK for isolated worker functions         |
| `Microsoft.ApplicationInsights.WorkerService`                 | 2.22.0  | Application Insights telemetry                  |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights`        | 2.0.0   | Application Insights integration for Functions  |
| `Microsoft.Extensions.Azure`                                  | 1.13.1  | Azure SDK dependency injection helpers          |

```bash
cd ResumePulse.Functions
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets --version 1.5.0
dotnet add package Azure.Identity --version 1.19.0
dotnet add package Azure.Messaging.EventGrid --version 5.0.0
dotnet add package Azure.Messaging.ServiceBus --version 7.20.1
dotnet add package Azure.Storage.Blobs --version 12.27.0
dotnet add package Google.GenAI --version 1.3.0
dotnet add package Microsoft.Azure.Functions.Worker --version 2.0.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.ServiceBus --version 5.24.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore --version 2.0.0
dotnet add package Microsoft.Azure.Functions.Worker.Sdk --version 2.0.0
dotnet add package Microsoft.ApplicationInsights.WorkerService --version 2.22.0
dotnet add package Microsoft.Azure.Functions.Worker.ApplicationInsights --version 2.0.0
dotnet add package Microsoft.Extensions.Azure --version 1.13.1
```

---

## 🚀 Local Development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- Azure subscription (for Key Vault, Service Bus, Storage)
- [Google AI Studio API Key](https://aistudio.google.com/)

### Run the API

```bash
cd ResumePulse.Api
dotnet run
# → http://localhost:5266
# → Swagger UI: http://localhost:5266/swagger
```

### Run the Blazor Frontend

```bash
cd ResumePulse.Client
dotnet run
```

### Run the Azure Function

```bash
cd ResumePulse.Functions
# Ensure local.settings.json is configured
func start
```

---

## ☁️ Azure Deployment

### 1. Provision Resources

```bash
# Resource Group
az group create --name rg-resumepulse --location centralindia

# Storage Account
az storage account create --name resumepulsestorage --resource-group rg-resumepulse --sku Standard_LRS

# Service Bus
az servicebus namespace create --name resumepulse-sb --resource-group rg-resumepulse --sku Basic
az servicebus queue create --name resume-analysis-queue --namespace-name resumepulse-sb --resource-group rg-resumepulse

# Key Vault
az keyvault create --name resumepulse-vault --resource-group rg-resumepulse --location centralindia

# Event Grid Custom Topic
az eventgrid topic create --name resumepulse-events --resource-group rg-resumepulse --location centralindia
```

### 2. Store Secrets in Key Vault

```bash
az keyvault secret set --vault-name resumepulse-vault --name StorageConnectionString --value "<value>"
az keyvault secret set --vault-name resumepulse-vault --name ServiceBusConnectionString --value "<value>"
```

### 3. Deploy API to App Service

```bash
cd ResumePulse.Api
dotnet publish -c Release -o ./publish
az webapp deploy --resource-group rg-resumepulse --name <app-service-name> --src-path ./publish
```

### 4. Deploy Frontend to Static Web Apps

```bash
# Step 1 — Install the Static Web Apps CLI (once)
npm install -g @azure/static-web-apps-cli

# Step 2 — Build the Blazor WASM app
cd ResumePulse.Client
dotnet publish -c Release -o ./publish

# Step 3 — Create the Static Web App resource and retrieve the deployment token
az staticwebapp create \
  --name resumepulse-frontend \
  --resource-group rg-resumepulse \
  --location centralindia \
  --sku Free

az staticwebapp secrets list \
  --name resumepulse-frontend \
  --resource-group rg-resumepulse \
  --query "properties.apiKey" -o tsv

# Step 4 — Deploy to production using the token from the step above
swa deploy ./publish/wwwroot \
  --deployment-token <token-from-step-3> \
  --env production

# Step 5 — Get the live URL
az staticwebapp show \
  --name resumepulse-frontend \
  --resource-group rg-resumepulse \
  --query "defaultHostname" -o tsv
```

### 5. Deploy Functions

```bash
cd ResumePulse.Functions
func azure functionapp publish <function-app-name>
```

---

## 🔑 Key Design Decisions

**Why Service Bus instead of direct Function call?**
Decoupling the upload from analysis allows independent scaling and provides built-in retry/dead-letter support.

**Why poll for the report instead of WebSocket/SignalR?**
Keeps the architecture simple for a POC. Event Grid integration is already in place for future push-based notifications.

**Why `DefaultAzureCredential`?**
Supports both local development (via Azure CLI login) and production (Managed Identity) without code changes.

**Why Gemini instead of Azure OpenAI?**
Free-tier availability makes it ideal for a portfolio POC. The `GeminiService` is interface-ready to swap in Azure OpenAI GPT-4o.

---

## 🧪 API Reference

| Method | Endpoint                     | Description                         |
| ------ | ---------------------------- | ----------------------------------- |
| `POST` | `/api/resume/upload`         | Upload resume PDF + job description |
| `GET`  | `/api/resume/report/{jobId}` | Poll for analysis report            |
| `GET`  | `/api/resume/health`         | Health check                        |
| `GET`  | `/swagger`                   | Interactive API docs                |

---

## 📈 Future Enhancements

- [ ] Switch Gemini to Azure OpenAI GPT-4o for full Azure-native stack
- [ ] Logic App subscriber on Event Grid for email delivery (SendGrid)
- [ ] SignalR push notifications instead of polling
- [ ] Resume history dashboard with Cosmos DB
- [ ] Multi-resume batch analysis
- [ ] Azure API Management gateway

---

## 👨‍💻 Author

**Pravin Manohar Kshirsagar** — .NET & Azure Developer at YASH Technologies  
Building towards AZ-204 / AZ-305 certification | [LinkedIn](https://www.linkedin.com/in/pravin-kshirsagar-567093229/) | [GitHub](https://github.com/Pravin223047)
