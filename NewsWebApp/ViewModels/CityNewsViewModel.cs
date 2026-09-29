using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    public class CityNewsViewModel
    {
        public string? City { get; set; }
        public IEnumerable<NewsArticle> Articles { get; set; } = new List<NewsArticle>();
        public IEnumerable<string> PopularCities { get; set; } = new List<string>
        {
            "Pune", "Jaipur", "Lucknow", "Chandigarh", "Ahmedabad",
            "Delhi", "Mumbai", "Bengaluru", "Chennai", "Hyderabad",
            "Kolkata", "Noida", "Gurgaon", "Patna", "Bhopal", "Indore", "Kochi"
        };
        public int TotalFound { get; set; }
        public bool IsLiveFetched { get; set; }
    }
}
