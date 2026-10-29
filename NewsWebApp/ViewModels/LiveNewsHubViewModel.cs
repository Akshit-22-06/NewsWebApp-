using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// Simple, beginner-friendly ViewModel for the Currents API Live News section in Admin.
    /// </summary>
    public class LiveNewsHubViewModel
    {
        public bool IsApiKeyConfigured { get; set; }
        public string BaseUrl { get; set; } = "https://api.currentsapi.services/";
        public string? SelectedCategory { get; set; }
        public string? SearchQuery { get; set; }
        public string? ErrorMessage { get; set; }
        public List<NewsDto> Articles { get; set; } = new();
    }
}
