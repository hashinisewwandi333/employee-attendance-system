using System.ComponentModel.DataAnnotations;

namespace EmployeeAttendanceSystem.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "නම")]
        public string? Name { get; set; }

        [Required]
        [Display(Name = "විද්‍යුත් තැපෑල")]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [Display(Name = "දුරකථන අංකය")]
        public string? Phone { get; set; }

        [Display(Name = "ලිපිනය")]
        public string? Address { get; set; }

        [Display(Name = "තනතුර")]
        public string? Position { get; set; }

        [Display(Name = "සේවයට බැඳුනු දිනය")]
        [DataType(DataType.Date)]
        public DateTime JoinDate { get; set; } = DateTime.Now;

        // Navigation property
        public ICollection<Attendance>? Attendances { get; set; }
    }
}
