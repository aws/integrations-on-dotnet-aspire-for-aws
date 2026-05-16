# SPA & API Playground

A full-stack todo application demonstrating how to deploy a .NET minimal API backend and an Angular SPA frontend to AWS using .NET Aspire and AWS CDK.

## Architecture

### Local Development

```text
┌─────────────────────────────────────────────────┐
│                 .NET Aspire AppHost             │
│                                                 │
│   ┌─────────────┐        ┌──────────────────┐   │
│   │   Frontend  │──────▶ │     Backend      │   │
│   │  (Vite/     │ proxy  │  (ASP.NET Core   │   │
│   │   Angular)  │ /todos │   Minimal API)   │   │
│   │  :3000      │        │  :5162           │   │
│   └─────────────┘        └──────────────────┘   │
└─────────────────────────────────────────────────┘
```

### AWS Deployment

```text
AWS (eu-central-1)
  │
  └── CloudFront Distribution
        │
        ├── /*           ──▶  S3 Bucket (Angular static assets)
        │                     dist/Frontend/browser
        │
        └── /todos/*     ──▶  Application Load Balancer
                                │
                                └── ECS Fargate (Backend container)
```

## Projects

| Project | Type | Description |
|---------|------|-------------|
| `AppHost` | .NET Aspire host | Orchestrates all services; defines AWS deployment topology |
| `Backend` | ASP.NET Core (.NET 10) | Minimal API exposing a thread-safe `/todos` REST endpoint |
| `Frontend` | Angular 21 + Vite | SPA consuming the backend; built to S3 on publish |

## API

```text
GET    /todos          List all todos
GET    /todos/{id}     Get a single todo
POST   /todos          Create a todo        { title, isCompleted }
PUT    /todos/{id}     Replace a todo       { title, isCompleted }
DELETE /todos/{id}     Delete a todo
```

The backend stores todos in a `ConcurrentDictionary` — safe for concurrent Fargate requests.

## Request Flow

### Local

```text
Browser → Angular Dev Server (:3000) → proxy /todos → Backend (:5162)
```

### AWS

```text
Browser → CloudFront → /todos/* → ALB → ECS Fargate (Backend)
                    └── /*      → S3  → Angular assets
```

## Running Locally

```bash
# from the SpaAndApi directory
aspire run
```

Aspire starts both services and opens the dashboard. The frontend is available at `http://localhost:3000`.

## Deploying to AWS

Prerequisites: AWS credentials configured for **eu-central-1**.

```bash
# from the SpaAndApi directory
aspire deploy
```

Aspire uses AWS CDK under the hood and provisions:

| Resource | Service |
|----------|---------|
| Frontend static assets | S3 bucket |
| CDN + routing | CloudFront distribution |
| `/todos/*` traffic | CloudFront → ALB behaviour |
| Backend API | ECS Fargate service + ALB |
| AWS environment | CDK bootstrap stack (`spa-and-api`) |
