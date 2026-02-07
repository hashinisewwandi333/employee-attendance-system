using EmployeeAttendanceSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using EmployeeAttendanceSystem.Data; // Add this
using Microsoft.EntityFrameworkCore; // Add this

namespace EmployeeAttendanceSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context; // Add this

        // Update constructor
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context; // Add this
        }

        public async Task<IActionResult> Index()
        {
            // Add statistics
            ViewBag.TotalEmployees = await _context.Employees.CountAsync();
            ViewBag.TotalAttendance = await _context.Attendances.CountAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Add this method to fix the error
        public IActionResult Create()
        {
            // Redirect to Employee Create page
            return RedirectToAction("Create", "Employee");
        }

        // Optional: Add About page
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}