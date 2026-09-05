---
name: genhttp
description: how to use genhttp effectively for http server
---

# GenHTTP Skill Guide

**GenHTTP** is a lightweight, high-performance HTTP server library for .NET that provides flexible tools for building REST APIs, web services, static websites, and real-time applications. Use this skill when a task involves setting up or managing HTTP servers, handling requests and responses, routing, middleware, server configuration, or other web server-related functionality.

## Quick Navigation

### 🚀 Getting Started
- [Two-Line Web Service (Minimal Example)](./docs/tutorials/two-line-webservice.md) — Quickest way to understand GenHTTP
- [Creating a Web Service (Detailed Tutorial)](./docs/tutorials/creating-a-webservice.md) — Step-by-step guide for building your first service
- [Main Documentation Index](./docs/_index.md) — Overview of all GenHTTP features

### 📐 Choose Your Framework

Select based on your use case:

- **[Web Services (REST APIs)](./docs/content/frameworks/webservices.md)** — Full-featured REST API implementation with routing and responses
- **[Functional Handlers](./docs/content/frameworks/functional.md)** — Minimal, single-function REST services for simple endpoints
- **[Controllers](./docs/content/frameworks/controllers.md)** — Class-based REST approach with organized methods
- **[WebSockets](./docs/content/frameworks/websockets.md)** — Real-time bidirectional communication
- **[Static Websites](./docs/content/frameworks/static-websites.md)** — Serving HTML, CSS, JavaScript files
- **[Single-Page Applications (SPAs)](./docs/content/frameworks/single-page-applications.md)** — Frontend framework integration (React, Vue, etc.)

### 🛠️ Building Applications

#### Routing & Request Handling
- [Routing](./docs/content/concepts/routing.md) — Define URL patterns and route requests
- [Definitions](./docs/content/concepts/definitions.md) — Method definitions and parameter handling
- [Response Content](./docs/content/concepts/response-content.md) — Crafting responses (JSON, HTML, etc.)
- [Resources](./docs/content/concepts/resources.md) — Managing request/response resources
- [Templates](./docs/content/templates.md) — Dynamic content generation

#### Handler Reference
Common handlers for specific tasks:

- [Layouting](./docs/content/handlers/layouting.md) — Combining multiple handlers into a hierarchy (fundamental)
- [Static Content](./docs/content/handlers/static-content.md) — Serve files and directories
- [Pages](./docs/content/handlers/pages.md) — Dynamic page serving
- [Content Handler](./docs/content/handlers/content.md) — Flexible content delivery
- [Listing](./docs/content/handlers/listing.md) — Directory/collection listings
- [Downloads](./docs/content/handlers/downloads.md) — File download handling
- [Redirects](./docs/content/handlers/redirects.md) — URL redirection
- [Server-Sent Events (SSE)](./docs/content/handlers/server-sent-events.md) — Server-to-client streaming
- [Reverse Proxy](./docs/content/handlers/reverse-proxy.md) — Proxy requests to other servers
- [Load Balancer](./docs/content/handlers/load-balancer.md) — Distribute requests across backends
- [Virtual Hosts](./docs/content/handlers/virtual-hosts.md) — Host multiple domains
- [API Browsing](./docs/content/handlers/api-browsing.md) — Auto-generated API explorer

#### Advanced Concepts
- [Dependency Injection](./docs/content/concepts/dependency-injection.md) — Service injection for handlers
- [Code Generation](./docs/content/concepts/code-generation.md) — Automatic code from definitions
- [Caches](./docs/content/concepts/caches.md) — Caching mechanisms
- [Content Overview](./docs/content/_index.md) — Complete content building guide

### ⚙️ Server Configuration & Deployment

- [Server Configuration Guide](./docs/server/_index.md) — Overview of server setup
- [Endpoints](./docs/server/endpoints.md) — Port and address configuration
- [Engines](./docs/server/engines.md) — Core server implementations
- [Adapters](./docs/server/adapters.md) — Protocol and platform adaptations
- [Companions](./docs/server/companions.md) — Supporting services
- [Security Configuration](./docs/server/security.md) — HTTPS, certificates, protocol security

### 🔒 Security & Performance Cross-Cutting Concerns

#### Authentication & Access Control
- [Authentication](./docs/content/concerns/authentication.md) — User authentication strategies
- [CORS (Cross-Origin Resource Sharing)](./docs/content/concerns/cors.md) — Handling cross-origin requests

#### Caching & Performance
- [Compression](./docs/content/concerns/compression.md) — Response compression (gzip, etc.)
- [Decompression](./docs/content/concerns/decompression.md) — Request decompression
- [Client Caching Policy](./docs/content/concerns/client-caching-policy.md) — Cache headers and directives
- [Client Caching Validation](./docs/content/concerns/client-caching-validation.md) — ETags and validation
- [Server-Side Caching](./docs/content/concerns/server-caching.md) — Application-level caching

#### Standards & Observability
- [OpenAPI](./docs/content/concerns/open-api.md) — API documentation generation
- [Range Support](./docs/content/concerns/range-support.md) — Partial content requests
- [Localization](./docs/content/concerns/localization.md) — Multi-language support
- [Error Handling](./docs/content/concerns/error-handling.md) — Exception handling and error responses
- [Inspection](./docs/content/concerns/inspection.md) — Monitoring and debugging
- [Concerns Overview](./docs/content/concerns/_index.md) — Complete concerns guide
- [Default Concerns](./docs/content/concerns/defaults.md) — Standard concern middleware

### 🧪 Testing & Quality
- [Testing Guide](./docs/testing/_index.md) — Testing approaches and best practices

---

## Quick Reference by Task

| Task | Resource |
|------|----------|
| Build REST API | [Web Services](./docs/content/frameworks/webservices.md) |
| Build minimal endpoint | [Functional Handlers](./docs/content/frameworks/functional.md) |
| Organize code with classes | [Controllers](./docs/content/frameworks/controllers.md) |
| Real-time updates | [WebSockets](./docs/content/frameworks/websockets.md) |
| Serve web files | [Static Websites](./docs/content/frameworks/static-websites.md) |
| React/Vue app | [Single-Page Applications](./docs/content/frameworks/single-page-applications.md) |
| Define routes | [Routing](./docs/content/concepts/routing.md) |
| Compress responses | [Compression](./docs/content/concerns/compression.md) |
| HTTPS/certificate | [Security](./docs/server/security.md) |
| Document API | [OpenAPI](./docs/content/concerns/open-api.md) |
| Cache management | [Caching](./docs/content/concerns/client-caching-policy.md) + [Server Cache](./docs/content/concerns/server-caching.md) |
| Custom middleware | [Concerns](./docs/content/concerns/_index.md) |
| Test server | [Testing](./docs/testing/_index.md) |
