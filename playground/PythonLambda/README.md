# Python Lambda Playground

Demonstrates using `AddAWSPythonLambdaFunction` to run a Python Lambda function
locally with the Aspire Lambda service emulator, DynamoDB Local, and the API
Gateway emulator — a full local e2e flow without deploying to AWS.

## Prerequisites

| Tool | Required version |
|------|-----------------|
| [.NET SDK](https://dot.net) | 8.0 or later |
| [Aspire workload](https://learn.microsoft.com/dotnet/aspire) | 9+ |
| Python | 3.11 or later |
| [AWS Lambda TestTool](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool-v2) | auto-installed by Aspire |
| Docker (for DynamoDB Local) | any recent version |

## Quick start

### 1. Set up the Python virtual environment

```bash
cd playground/PythonLambda/ToUpperLambda
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
```

`Aspire.Hosting.AWS` automatically uses the `.venv` in `ToUpperLambda/` when
it starts the Lambda process. If no `.venv` is present it falls back to the
system `python3`.

### 2. Run the Aspire AppHost

```bash
cd playground/PythonLambda/PythonLambda.AppHost
dotnet run
```

The Aspire dashboard opens in your browser. You should see:
- **LambdaServiceEmulator** – the Lambda Runtime API proxy
- **ToUpperFunction** – the Python process running `awslambdaric`
- **DynamoDBLocal** – the local DynamoDB container
- **APIGatewayEmulator** – the HTTP API Gateway proxy

### 3. Invoke the function

```bash
curl -X POST http://localhost:<api-gateway-port>/to-upper \
  -H "Content-Type: text/plain" \
  -d "hello world"
```

Expected response:
```json
{"result": "HELLO WORLD", "invocationCount": 1}
```

Each call increments `invocationCount` in the DynamoDB Local `InvocationCounters` table.

### 4. Use the Lambda Test Tool UI

Click the **Lambda Service Emulator** link in the Aspire dashboard (or the
_Lambda Test Tool UI_ link on the ToUpperFunction resource) to open the browser UI
where you can send arbitrary payloads directly to the function.

## How it works

1. `AddAWSPythonLambdaFunction` creates an `ExecutableResource` that runs
   `python -m awslambdaric` inside the `ToUpperLambda/` directory.
2. Aspire injects `AWS_LAMBDA_RUNTIME_API`, `_HANDLER`, and
   `AWS_LAMBDA_FUNCTION_NAME` environment variables to connect the process to
   the Lambda service emulator.
3. `WithReference(dynamoDBLocal)` injects `AWS_ENDPOINT_URL_DYNAMODB` so
   `boto3` transparently hits DynamoDB Local instead of AWS.
4. The API Gateway emulator routes `POST /to-upper` to the function via the
   Lambda service emulator.

## Playground structure

```
PythonLambda/
├── PythonLambda.AppHost/   ← C# Aspire AppHost
│   ├── Program.cs
│   └── PythonLambda.AppHost.csproj
└── ToUpperLambda/          ← Python Lambda source
    ├── main.py             ← handler: main.handler
    └── requirements.txt    ← awslambdaric, boto3
```
