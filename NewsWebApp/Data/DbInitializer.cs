using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NewsWebApp.Models;

namespace NewsWebApp.Data
{
    /// <summary>
    /// Simple and clean database initializer.
    /// Seeds initial roles, demo users, categories, and starter articles if the database is empty.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // 1. Ensure database schema is created
            await context.Database.EnsureCreatedAsync();

            // 2. Seed Identity Roles
            string[] roleNames = { "Admin", "Author", "Reader" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 3. Seed Default Admin User
            var adminUser = await userManager.FindByEmailAsync("admin@newswebapp.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@newswebapp.com",
                    Email = "admin@newswebapp.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 4. Seed Default Author User & Author Profile
            var authorUser = await userManager.FindByEmailAsync("author@newswebapp.com");
            if (authorUser == null)
            {
                authorUser = new ApplicationUser
                {
                    UserName = "author@newswebapp.com",
                    Email = "author@newswebapp.com",
                    FullName = "Jane Doe",
                    Bio = "Senior investigative journalist specializing in enterprise tech and science.",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(authorUser, "Author@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(authorUser, "Author");
                }
            }

            // Ensure Author entity exists for authorUser
            Author? author = await context.Authors.FirstOrDefaultAsync(a => a.UserId == authorUser.Id);
            if (author == null)
            {
                author = new Author
                {
                    UserId = authorUser.Id,
                    DisplayName = "Jane Doe",
                    Bio = "Senior investigative journalist specializing in enterprise tech and science.",
                    ProfileImageUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150"
                };
                context.Authors.Add(author);
                await context.SaveChangesAsync();
            }

            // 5. Seed Default Reader User
            var readerUser = await userManager.FindByEmailAsync("reader@newswebapp.com");
            if (readerUser == null)
            {
                readerUser = new ApplicationUser
                {
                    UserName = "reader@newswebapp.com",
                    Email = "reader@newswebapp.com",
                    FullName = "Alex Morgan",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(readerUser, "Reader@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(readerUser, "Reader");
                }
            }

            // 6. Seed Categories
            if (!await context.Categories.AnyAsync())
            {
                var categories = new List<Category>
                {
                    new() { Name = "World", Slug = "world", Description = "Global diplomatic, environmental, and international affairs." },
                    new() { Name = "Technology", Slug = "technology", Description = "Artificial intelligence, software systems, cybersecurity, and hardware." },
                    new() { Name = "Business", Slug = "business", Description = "Global economics, markets, corporate governance, and startups." },
                    new() { Name = "Science", Slug = "science", Description = "Astrophysics, clean energy, quantum mechanics, and medical discoveries." },
                    new() { Name = "Sports", Slug = "sports", Description = "International championships, tournaments, analytics, and athletic news." }
                };
                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }

            // 7. Seed Starter News Articles if empty
            if (!await context.NewsArticles.AnyAsync())
            {
                var techCategory = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "technology");
                var worldCategory = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "world");
                var scienceCategory = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "science");

                var sampleArticles = new List<NewsArticle>
                {
                    new()
                    {
                        Title = "Next-Generation Open Source AI Models Achieve New Benchmarks",
                        Slug = "next-gen-open-source-ai-models-benchmarks",
                        Summary = "Recent automated benchmarking shows decentralized open weights matching proprietary models across programming and logic.",
                        Content = "Recent open-source AI advancements have dramatically narrowed the gap between private and public intelligence models. Developers worldwide are now able to run high-performance reasoning locally.",
                        ImageUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=900",
                        CategoryId = techCategory?.Id ?? 1,
                        AuthorId = author.Id,
                        IsPublished = true,
                        IsFeatured = true,
                        PublishedDate = DateTime.UtcNow.AddHours(-2),
                        CreatedAt = DateTime.UtcNow.AddHours(-2),
                        ViewCount = 120,
                        LikeCount = 14
                    },
                    new()
                    {
                        Title = "Global Climate Accord Reaches Historic Renewable Energy Milestone",
                        Slug = "global-climate-accord-renewable-milestone",
                        Summary = "Renewable energy production surpassed 40% of peak continental grids across international monitoring stations this quarter.",
                        Content = "International environmental agencies released their annual summary today, demonstrating rapid acceleration in solar and wind adoption across developing and industrialized nations alike.",
                        ImageUrl = "https://images.unsplash.com/photo-1473341304170-971dccb5ac1e?w=900",
                        CategoryId = worldCategory?.Id ?? 1,
                        AuthorId = author.Id,
                        IsPublished = true,
                        IsFeatured = false,
                        PublishedDate = DateTime.UtcNow.AddHours(-5),
                        CreatedAt = DateTime.UtcNow.AddHours(-5),
                        ViewCount = 75,
                        LikeCount = 8
                    },
                    new()
                    {
                        Title = "Deep-Space Telescope Discovers Atmospheric Water on Exoplanet",
                        Slug = "deep-space-telescope-exoplanet-atmosphere",
                        Summary = "Spectroscopic analysis reveals clear chemical indicators of water vapor and atmospheric equilibrium on a nearby super-Earth.",
                        Content = "Astrophysicists utilizing space-based infrared observatories have confirmed high-altitude vapor lines surrounding a terrestrial exoplanet located 48 light-years away.",
                        ImageUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=900",
                        CategoryId = scienceCategory?.Id ?? 1,
                        AuthorId = author.Id,
                        IsPublished = true,
                        IsFeatured = false,
                        PublishedDate = DateTime.UtcNow.AddHours(-10),
                        CreatedAt = DateTime.UtcNow.AddHours(-10),
                        ViewCount = 95,
                        LikeCount = 11
                    }
                };

                context.NewsArticles.AddRange(sampleArticles);
                await context.SaveChangesAsync();
            }

            // 8. Seed Default Feed Sources if empty
            if (!await context.NewsFeedSources.AnyAsync())
            {
                context.NewsFeedSources.AddRange(
                    new NewsFeedSource { Name = "BBC World", CategorySlug = "world", FeedUrl = "https://feeds.bbci.co.uk/news/world/rss.xml", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new NewsFeedSource { Name = "TechCrunch", CategorySlug = "technology", FeedUrl = "https://techcrunch.com/feed/", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new NewsFeedSource { Name = "BBC Business", CategorySlug = "business", FeedUrl = "https://feeds.bbci.co.uk/news/business/rss.xml", IsActive = true, CreatedAt = DateTime.UtcNow }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
