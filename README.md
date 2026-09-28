# NewsSphere — ASP.NET Core MVC News Web Application

A complete, production-ready **News Web Application** built using **C#, ASP.NET Core MVC, Entity Framework Core, ASP.NET Core Identity, and the Repository Pattern**.

Designed with clean architecture, strong separation of concerns, dependency injection, role-based authorization, and slug-based URLs. The frontend uses **only semantic HTML, Razor Views, Razor Tag Helpers, and a custom Vanilla CSS design system (`site.css`)** — no external UI frameworks (Bootstrap, Tailwind, or jQuery) were used.

---

## 📖 Complete Project Documentation

For the complete, in-depth architectural guide, database schema ER diagrams, repository details, routing explanations, and controller matrix, see:

👉 **[Complete Project Technical Documentation](file:///Users/akshitsharma/Antigravity_Wad_project/NewsWebApp/PROJECT_DOCUMENTATION.md)**

---

## 🚀 Quick Start Guide

### 1. Navigate to the project directory:
```bash
cd NewsWebApp
```

### 2. Run the application:
```bash
dotnet run
```

### 3. Open in your browser:
Navigate to `http://localhost:5235`

---

## 🔑 Pre-Configured Test Accounts

| Role | Email | Password | Permissions |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@newswebapp.com` | `Admin@123456` | Full platform control, moderation, category management, user audit |
| **Author** | `author@newswebapp.com` | `Author@123456` | Authoring studio, article CRUD for own articles |
| **Reader** | `reader@newswebapp.com` | `Reader@123456` | Article reading, commenting, self-comment deletion |

---

## ⚡ Real-Time Live News Ingestion Engine & Multi-API Hub

NewsSphere features an enterprise-grade **multi-provider news ingestion engine**:
- **Official Hacker News REST API:** Zero API key required, 100% free, real-time top technology and AI dispatches.
- **Global Syndicate XML Feeds:** Ingests live breaking stories from **BBC World News, TechCrunch, The Verge, BBC Business, NASA Breaking News, BBC Science, ESPN, and BBC Politics**.
- **REST News APIs:** First-class support for **GNews API (gnews.io)** and **NewsAPI.org**. Add your keys in `appsettings.json` under `LiveNews:GNewsApiKey` and `LiveNews:NewsApiKey`.
- **Dynamic Custom Feed Manager:** Administrators can add ANY custom RSS/Atom XML feed directly from the `/admin/livenews` UI to ingest into any category.
- **Automated Background Poller:** Background worker (`LiveNewsSyncBackgroundService`) polls periodically (default: every 60 minutes).
- **Intelligent Deduplication:** Matches title and source URL to prevent duplicate entries.
- **Source Attribution & LIVE WIRE Badges:** Clean badges and links to original publisher articles.

---

## 🌟 Rich Interactive Features

1. **Breaking News Live Ticker Ribbon:** Continuous marquee ribbon at the top of the site displaying live dispatches with a blinking pulse indicator and pause-on-hover.
2. **Article Bookmarks & Personal Reading List:** One-click "Save for Later" for logged-in readers, with dedicated reading queue at `/news/bookmarks`.
3. **Article Reaction / Like System:** Interactive like counter with immediate feedback and database persistence.
4. **Reader Comfort & Accessibility Toolbar:**
   - Text size adjuster (`A-`, `A`, `A+`) to resize article typography.
   - Dynamic estimated reading time badge (e.g. `⏱️ 4 min read`).
   - One-click copy article link with toast notification.
   - Social share shortcuts (X/Twitter, LinkedIn, and clean `@media print` mode).
5. **Morning Intelligence Briefing Newsletter:** Newsletter subscription card on the homepage and footer, with an Admin subscriber registry at `/admin/subscribers`.
6. **Advanced Newsroom Search & Filter:** Filter by keywords, Category, Source Type ("All", "⚡ Live Wire Only", "✍️ Editorial Staff"), Date range ("Today", "Past Week", "Past Month"), and Sort ("Latest", "Most Viewed", "Most Liked", "Most Discussed").
7. **Most Discussed Stories:** Curated by total reader comment counts.

---

## 🛠️ Technology Stack
- **C# & ASP.NET Core MVC** (.NET 10 / LTS compatible)
- **Entity Framework Core Code-First**
- **Microsoft SQL Server Provider** (`Microsoft.EntityFrameworkCore.SqlServer`)
- **SQLite Fallback Provider** (`Microsoft.EntityFrameworkCore.Sqlite`)
- **ASP.NET Core Identity**
- **Semantic HTML5 & Pure Vanilla CSS3** (Zero Bootstrap, Tailwind, or jQuery)
- **ASP.NET Core Identity** (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`)
- **Repository Pattern** (`INewsRepository`, `ICategoryRepository`, `ICommentRepository`, `IAuthorRepository`)
- **Real-Time News Synchronization Engine** (`ILiveNewsService`, `LiveNewsSyncBackgroundService`)
- **Pure Vanilla CSS** (`wwwroot/css/site.css`)
- **SEO-Friendly Slug Routing** (`/news/details/{slug}`, `/news/category/{slug}`)
