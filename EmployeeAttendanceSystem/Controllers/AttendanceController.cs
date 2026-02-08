using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeAttendanceSystem.Data;
using EmployeeAttendanceSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Diagnostics; // Add for null forgiving operator

namespace EmployeeAttendanceSystem.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================ SUMMARY PAGE (Dashboard) ================
        public async Task<IActionResult> Summary()
        {
            try
            {
                var today = DateTime.Today;
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                // Get today's attendance
                var todaysAttendance = await _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.Date == today)
                    .ToListAsync();

                // Get current month statistics
                var startDate = new DateTime(currentYear, currentMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var monthlyAttendance = await _context.Attendances
                    .Where(a => a.Date >= startDate && a.Date <= endDate)
                    .ToListAsync();

                // Get employee count
                var totalEmployees = await _context.Employees.CountAsync();

                // Calculate statistics
                var presentToday = todaysAttendance.Count(a => a.Status == "Present" || a.Status == "Late");
                var absentToday = totalEmployees - presentToday;

                var totalMonthlyRecords = monthlyAttendance.Count;
                var presentMonthly = monthlyAttendance.Count(a => a.Status == "Present" || a.Status == "Late");
                var attendanceRate = totalEmployees > 0 ?
                    Math.Round((double)presentToday / totalEmployees * 100, 2) : 0;

                // Pass data to view
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.TotalRecords = await _context.Attendances.CountAsync();
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMM yyyy");
                ViewBag.TodaysDate = DateTime.Now.ToString("dd-MM-yyyy");
                ViewBag.PresentToday = presentToday;
                ViewBag.AbsentToday = absentToday;
                ViewBag.AttendanceRate = attendanceRate;
                ViewBag.TodaysAttendance = todaysAttendance;
                ViewBag.MonthlyTotal = totalMonthlyRecords;

                return View();
            }
            catch (Exception)
            {
                // Fallback values in case of error
                ViewBag.TotalEmployees = 3;
                ViewBag.TotalRecords = 0;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMM yyyy");
                ViewBag.TodaysDate = DateTime.Now.ToString("dd-MM-yyyy");
                ViewBag.PresentToday = 0;
                ViewBag.AbsentToday = 3;
                ViewBag.AttendanceRate = 0;
                ViewBag.TodaysAttendance = new List<Attendance>();
                ViewBag.MonthlyTotal = 0;

                return View();
            }
        }

        // ================ INDEX PAGE ================
        public async Task<IActionResult> Index()
        {
            try
            {
                var attendances = await _context.Attendances
                    .Include(a => a.Employee)
                    .OrderByDescending(a => a.Date)
                    .ThenByDescending(a => a.CheckIn)
                    .ToListAsync();

                return View(attendances);
            }
            catch (Exception)
            {
                return View(new List<Attendance>());
            }
        }

        // ================ CREATE (GET) ================
        public async Task<IActionResult> Create()
        {
            // AUTO-CREATE EMPLOYEES IF DATABASE IS EMPTY
            try
            {
                if (!await _context.Employees.AnyAsync())
                {
                    _context.Employees.AddRange(
                        new Employee { Name = "Hashini Sewwandi", Position = "Web Developer" },
                        new Employee { Name = "Kasun Dananjaya", Position = "Software Engineer" },
                        new Employee { Name = "Nimal Perera", Position = "Manager" }
                    );
                    await _context.SaveChangesAsync();

                    TempData["Message"] = "✅ Default employees created successfully!";
                }
            }
            catch (Exception)
            {
                // Silent fail
            }

            await LoadEmployeesDropdownFixed();
            ViewBag.Today = DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.CurrentTime = DateTime.Now.ToString("HH:mm");

            return View();
        }

        // ================ CREATE (POST) ================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Attendance attendance)
        {
            try
            {
                // FIX: Check for null
                if (attendance == null)
                {
                    ModelState.AddModelError("", "Attendance data is null.");
                    await LoadEmployeesDropdownFixed();
                    return View(new Attendance());
                }

                var employeeExists = await _context.Employees
                    .AnyAsync(e => e.Id == attendance.EmployeeId);

                if (!employeeExists)
                {
                    ModelState.AddModelError("EmployeeId", "Selected employee does not exist.");
                    await LoadEmployeesDropdownFixed();
                    return View(attendance);
                }

                // FIX: Ensure Date is set
                if (attendance.Date == DateTime.MinValue)
                {
                    attendance.Date = DateTime.Today;
                }

                // Combine Date with Time for CheckIn
                if (attendance.CheckIn != DateTime.MinValue)
                {
                    TimeSpan checkInTime = attendance.CheckIn.TimeOfDay;
                    attendance.CheckIn = attendance.Date.Date + checkInTime;
                }
                else
                {
                    attendance.CheckIn = attendance.Date.Date + new TimeSpan(8, 30, 0);
                }

                // Combine Date with Time for CheckOut
                if (attendance.CheckOut.HasValue && attendance.CheckOut != DateTime.MinValue)
                {
                    TimeSpan checkOutTime = attendance.CheckOut.Value.TimeOfDay;
                    attendance.CheckOut = attendance.Date.Date + checkOutTime;
                }
                else
                {
                    attendance.CheckOut = null;
                }

                attendance.Date = attendance.Date.Date;

                // Calculate Status
                if (attendance.CheckIn.TimeOfDay > new TimeSpan(8, 30, 0))
                {
                    attendance.Status = "Late";
                }
                else
                {
                    attendance.Status = "Present";
                }

                // FIX: Set default status if empty
                if (string.IsNullOrEmpty(attendance.Status))
                {
                    attendance.Status = "Present";
                }

                if (ModelState.IsValid)
                {
                    _context.Attendances.Add(attendance);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "✅ Attendance saved successfully!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                if (errorMessage.Contains("foreign key") || errorMessage.Contains("EmployeeId"))
                {
                    ModelState.AddModelError("EmployeeId",
                        "Employee not found. Please add employee first.");
                }
                else
                {
                    ModelState.AddModelError("", $"Database error: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            await LoadEmployeesDropdownFixed();
            return View(attendance);
        }

        // ================ CALENDAR VIEW ================
        public async Task<IActionResult> Calendar(int? year, int? month, int? employeeId)
        {
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            ViewBag.CurrentYear = currentYear;
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.EmployeeId = employeeId;

            // Get employees for dropdown
            try
            {
                var employees = await _context.Employees.ToListAsync();
                ViewBag.Employees = employees ?? new List<Employee>();
            }
            catch (Exception)
            {
                ViewBag.Employees = new List<Employee>();
            }

            // Get attendance data
            var startDate = new DateTime(currentYear, currentMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var query = _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.Date >= startDate && a.Date <= endDate);

            if (employeeId.HasValue)
            {
                query = query.Where(a => a.EmployeeId == employeeId.Value);
            }

            var attendanceData = await query.ToListAsync();

            return View(attendanceData);
        }

        // ================ REPORTS PAGE ================
        public async Task<IActionResult> Reports(int? employeeId, int? month, int? year)
        {
            try
            {
                // Default to current month/year
                var reportYear = year ?? DateTime.Now.Year;
                var reportMonth = month ?? DateTime.Now.Month;

                ViewBag.SelectedYear = reportYear;
                ViewBag.SelectedMonth = reportMonth;
                ViewBag.EmployeeId = employeeId;

                // Months for dropdown
                ViewBag.Months = new List<SelectListItem>
                {
                    new SelectListItem { Value = "1", Text = "January" },
                    new SelectListItem { Value = "2", Text = "February" },
                    new SelectListItem { Value = "3", Text = "March" },
                    new SelectListItem { Value = "4", Text = "April" },
                    new SelectListItem { Value = "5", Text = "May" },
                    new SelectListItem { Value = "6", Text = "June" },
                    new SelectListItem { Value = "7", Text = "July" },
                    new SelectListItem { Value = "8", Text = "August" },
                    new SelectListItem { Value = "9", Text = "September" },
                    new SelectListItem { Value = "10", Text = "October" },
                    new SelectListItem { Value = "11", Text = "November" },
                    new SelectListItem { Value = "12", Text = "December" }
                };

                // Years for dropdown
                var currentYearValue = DateTime.Now.Year;
                var years = new List<SelectListItem>();
                for (int y = currentYearValue - 2; y <= currentYearValue + 1; y++)
                {
                    years.Add(new SelectListItem { Value = y.ToString(), Text = y.ToString() });
                }
                ViewBag.Years = years;

                // Employees for dropdown
                var employeesList = await _context.Employees
                    .OrderBy(e => e.Name)
                    .Select(e => new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = $"{e.Name} - {e.Position}"
                    })
                    .ToListAsync();

                // FIX: Null check for employeesList
                employeesList ??= new List<SelectListItem>();

                employeesList.Insert(0, new SelectListItem { Value = "", Text = "All Employees" });
                ViewBag.EmployeeList = employeesList;

                // Get attendance data
                var startDate = new DateTime(reportYear, reportMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var query = _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.Date >= startDate && a.Date <= endDate);

                // FIXED: Handle null reference for selectedEmployee
                if (employeeId.HasValue)
                {
                    query = query.Where(a => a.EmployeeId == employeeId.Value);
                    var selectedEmployee = await _context.Employees.FindAsync(employeeId.Value);
                    ViewBag.SelectedEmployeeName = selectedEmployee != null ?
                        selectedEmployee.Name : "Unknown Employee";
                }
                else
                {
                    ViewBag.SelectedEmployeeName = "All Employees";
                }

                var attendanceData = await query
                    .OrderBy(a => a.Date)
                    .ThenBy(a => a.Employee != null ? a.Employee.Name : "")
                    .ToListAsync();

                // Calculate statistics
                ViewBag.TotalRecords = attendanceData.Count;
                var totalEmployees = await _context.Employees.CountAsync();
                ViewBag.TotalEmployees = totalEmployees;

                if (attendanceData.Any())
                {
                    var presentCount = attendanceData.Count(a =>
                        a.Status == "Present" || a.Status == "Late");
                    var absentCount = attendanceData.Count(a => a.Status == "Absent");
                    var lateCount = attendanceData.Count(a => a.Status == "Late");
                    var earlyCount = attendanceData.Count(a => a.Status == "Present");

                    ViewBag.PresentCount = presentCount;
                    ViewBag.AbsentCount = absentCount;
                    ViewBag.LateCount = lateCount;
                    ViewBag.EarlyCount = earlyCount;

                    // Calculate attendance rate
                    var totalDaysInMonth = DateTime.DaysInMonth(reportYear, reportMonth);
                    var totalPossibleAttendance = employeeId.HasValue ?
                        totalDaysInMonth :
                        totalDaysInMonth * totalEmployees;

                    ViewBag.AttendanceRate = totalPossibleAttendance > 0 ?
                        Math.Round((double)presentCount / totalPossibleAttendance * 100, 2) : 0;

                    ViewBag.TotalDaysInMonth = totalDaysInMonth;
                }
                else
                {
                    ViewBag.PresentCount = 0;
                    ViewBag.AbsentCount = 0;
                    ViewBag.LateCount = 0;
                    ViewBag.EarlyCount = 0;
                    ViewBag.AttendanceRate = 0;
                    ViewBag.TotalDaysInMonth = DateTime.DaysInMonth(reportYear, reportMonth);
                }

                return View(attendanceData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading reports: {ex.Message}";
                return View(new List<Attendance>());
            }
        }

        // ================ MONTHLY SUMMARY PAGE ================
        public async Task<IActionResult> MonthlySummary(int? year, int? month)
        {
            try
            {
                // Default to current month/year
                var reportYear = year ?? DateTime.Now.Year;
                var reportMonth = month ?? DateTime.Now.Month;

                ViewBag.SelectedYear = reportYear;
                ViewBag.SelectedMonth = reportMonth;
                ViewBag.CurrentMonthName = new DateTime(reportYear, reportMonth, 1).ToString("MMMM yyyy");

                // Get the start and end dates for the month
                var startDate = new DateTime(reportYear, reportMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Get all employees
                var employees = await _context.Employees
                    .OrderBy(e => e.Name)
                    .ToListAsync();

                // Get attendance data for the month
                var attendanceData = await _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.Date >= startDate && a.Date <= endDate)
                    .ToListAsync();

                // Create summary for each employee
                var monthlySummary = new List<MonthlySummaryVM>();

                foreach (var employee in employees)
                {
                    var employeeAttendance = attendanceData
                        .Where(a => a.EmployeeId == employee.Id)
                        .ToList();

                    var totalDaysInMonth = DateTime.DaysInMonth(reportYear, reportMonth);
                    var presentDays = employeeAttendance.Count(a =>
                        a.Status == "Present" || a.Status == "Late");
                    var absentDays = employeeAttendance.Count(a => a.Status == "Absent");
                    var lateDays = employeeAttendance.Count(a => a.Status == "Late");
                    var earlyDays = employeeAttendance.Count(a => a.Status == "Present");

                    // Calculate attendance percentage
                    var attendancePercentage = totalDaysInMonth > 0 ?
                        Math.Round((double)presentDays / totalDaysInMonth * 100, 2) : 0;

                    // Calculate total working hours
                    var totalHours = employeeAttendance
                        .Where(a => a.CheckOut.HasValue)
                        .Sum(a => (a.CheckOut!.Value - a.CheckIn).TotalHours);

                    monthlySummary.Add(new MonthlySummaryVM
                    {
                        EmployeeId = employee.Id,
                        EmployeeName = employee.Name ?? string.Empty,    // FIX: Null check
                        Position = employee.Position ?? string.Empty,    // FIX: Null check
                        TotalDays = totalDaysInMonth,
                        PresentDays = presentDays,
                        AbsentDays = absentDays,
                        LateDays = lateDays,
                        EarlyDays = earlyDays,
                        AttendancePercentage = attendancePercentage,
                        TotalWorkingHours = Math.Round(totalHours, 2)
                    });
                }

                // Calculate overall statistics
                ViewBag.TotalEmployees = employees.Count;
                ViewBag.TotalPresentDays = monthlySummary.Sum(m => m.PresentDays);
                ViewBag.TotalAbsentDays = monthlySummary.Sum(m => m.AbsentDays);
                ViewBag.TotalLateDays = monthlySummary.Sum(m => m.LateDays);
                ViewBag.AverageAttendance = monthlySummary.Any() ?
                    Math.Round(monthlySummary.Average(m => m.AttendancePercentage), 2) : 0;

                // Pass months and years for dropdown
                ViewBag.Months = new List<SelectListItem>
                {
                    new SelectListItem { Value = "1", Text = "January" },
                    new SelectListItem { Value = "2", Text = "February" },
                    new SelectListItem { Value = "3", Text = "March" },
                    new SelectListItem { Value = "4", Text = "April" },
                    new SelectListItem { Value = "5", Text = "May" },
                    new SelectListItem { Value = "6", Text = "June" },
                    new SelectListItem { Value = "7", Text = "July" },
                    new SelectListItem { Value = "8", Text = "August" },
                    new SelectListItem { Value = "9", Text = "September" },
                    new SelectListItem { Value = "10", Text = "October" },
                    new SelectListItem { Value = "11", Text = "November" },
                    new SelectListItem { Value = "12", Text = "December" }
                };

                var currentYear = DateTime.Now.Year;
                var years = new List<SelectListItem>();
                for (int y = currentYear - 2; y <= currentYear + 1; y++)
                {
                    years.Add(new SelectListItem { Value = y.ToString(), Text = y.ToString() });
                }
                ViewBag.Years = years;

                return View(monthlySummary);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading monthly summary: {ex.Message}";
                return View(new List<MonthlySummaryVM>());
            }
        }

        // ================ EXPORT TO PDF/EXCEL ================
        public async Task<IActionResult> ExportReport(string format, int? employeeId, int? month, int? year)
        {
            try
            {
                var reportYear = year ?? DateTime.Now.Year;
                var reportMonth = month ?? DateTime.Now.Month;

                var startDate = new DateTime(reportYear, reportMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var query = _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.Date >= startDate && a.Date <= endDate);

                if (employeeId.HasValue)
                {
                    query = query.Where(a => a.EmployeeId == employeeId.Value);
                }

                var reportData = await query
                    .OrderBy(a => a.Date)
                    .ThenBy(a => a.Employee != null ? a.Employee.Name : "")
                    .ToListAsync();

                if (format == "excel")
                {
                    TempData["Success"] = "Excel export feature coming soon!";
                }
                else if (format == "pdf")
                {
                    TempData["Success"] = "PDF export feature coming soon!";
                }

                return RedirectToAction(nameof(Reports), new { employeeId, month, year });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Export error: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        // ================ AUTO-FIX DATABASE ================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoFixDatabase()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();

                if (!await _context.Employees.AnyAsync())
                {
                    _context.Employees.AddRange(
                        new Employee { Name = "Hashini Sewwandi", Position = "Web Developer" },
                        new Employee { Name = "Kasun Dananjaya", Position = "Software Engineer" },
                        new Employee { Name = "Nimal Perera", Position = "Manager" }
                    );
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "✅ Database fixed successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Error: {ex.Message}";
            }

            return RedirectToAction(nameof(Create));
        }

        // ================ DELETE ATTENDANCE ================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var attendance = await _context.Attendances.FindAsync(id);
                if (attendance != null)
                {
                    _context.Attendances.Remove(attendance);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Attendance deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Attendance not found.";
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "Error deleting attendance.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ================ FIXED HELPER METHOD ================
        private async Task LoadEmployeesDropdownFixed()
        {
            try
            {
                var employees = await _context.Employees
                    .OrderBy(e => e.Name)
                    .Select(e => new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = $"{e.Name} ({e.Position})"
                    })
                    .ToListAsync();

                // FIX: Use null-coalescing operator
                employees ??= new List<SelectListItem>();

                // FIX: Check for empty list
                if (!employees.Any())
                {
                    employees = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "1", Text = "Hashini Sewwandi (Web Developer)" },
                        new SelectListItem { Value = "2", Text = "Kasun Dananjaya (Software Engineer)" },
                        new SelectListItem { Value = "3", Text = "Nimal Perera (Manager)" }
                    };
                }

                ViewBag.Employees = employees;
            }
            catch (Exception)
            {
                // Fallback hardcoded values
                ViewBag.Employees = new List<SelectListItem>
                {
                    new SelectListItem { Value = "1", Text = "Hashini Sewwandi (Web Developer)" },
                    new SelectListItem { Value = "2", Text = "Kasun Dananjaya (Software Engineer)" },
                    new SelectListItem { Value = "3", Text = "Nimal Perera (Manager)" }
                };
            }
        }

        // ================ VIEW MODEL FOR MONTHLY SUMMARY ================
        public class MonthlySummaryVM
        {
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty; // FIX: Initialize with default
            public string Position { get; set; } = string.Empty;     // FIX: Initialize with default
            public int TotalDays { get; set; }
            public int PresentDays { get; set; }
            public int AbsentDays { get; set; }
            public int LateDays { get; set; }
            public int EarlyDays { get; set; }
            public double AttendancePercentage { get; set; }
            public double TotalWorkingHours { get; set; }
        }
    }
}