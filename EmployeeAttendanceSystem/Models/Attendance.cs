using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmployeeAttendanceSystem.Models
{
    public class Attendance
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Check In time is required")]
        [DataType(DataType.Time)]
        [Display(Name = "Check In Time")]
        public DateTime CheckIn { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Check Out Time")]
        public DateTime? CheckOut { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [StringLength(50)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Present";

        [StringLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // ✅ **තාවකාලිකව මේ properties ටික REMOVE කරන්න**
        // කිසිදු [NotMapped] property එකක් නොතියන්න!

        // Constructor
        public Attendance()
        {
            Status = "Present";
        }
    }
}