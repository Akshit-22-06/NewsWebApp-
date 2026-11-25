using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Services;

namespace NewsWebApp.Controllers
{
    public class NewsletterController : Controller
    {
        private readonly INewsletterRepository _newsletterRepository;
        private readonly INewsletterSchedulerManager _schedulerManager;

        public NewsletterController(
            INewsletterRepository newsletterRepository,
            INewsletterSchedulerManager schedulerManager)
        {
            _newsletterRepository = newsletterRepository;
            _schedulerManager = schedulerManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email, string? returnUrl)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var (success, message) = await _schedulerManager.SubscribeAndSendWelcomeAsync(email, baseUrl);

            // If AJAX request, return structured JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success, message });
            }

            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Unsubscribe(string? token)
        {
            ViewData["Title"] = "Newsletter Preferences — NewsBase";

            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Success = false;
                ViewBag.Message = "Invalid or missing unsubscribe token. Please use the personalized link provided in your email.";
                return View();
            }

            var (success, message, subscriber) = await _newsletterRepository.UnsubscribeByTokenAsync(token);
            ViewBag.Success = success;
            ViewBag.Message = message;
            ViewBag.Subscriber = subscriber;
            ViewBag.Token = token;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resubscribe(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid token.";
                return RedirectToAction("Index", "Home");
            }

            var subscriber = await _newsletterRepository.GetByTokenAsync(token);
            if (subscriber == null)
            {
                TempData["ErrorMessage"] = "Subscriber record not found.";
                return RedirectToAction("Index", "Home");
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var (success, message) = await _schedulerManager.SubscribeAndSendWelcomeAsync(subscriber.Email, baseUrl);
            TempData["SuccessMessage"] = "You have been resubscribed to The Morning Dispatch. Welcome back!";
            return RedirectToAction(nameof(Unsubscribe), new { token });
        }
    }
}
