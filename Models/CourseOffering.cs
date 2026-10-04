using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication_Credit_hour_system.Models;



namespace WebApplication_Credit_hour_system.Models
{

    [Table("CourseOffering")]
    public class CourseOffering
    {
        [Key]
        public int OfferingID { get; set; }

        [Required]
        public string CourseID { get; set; }
        // [Required]
        // public string CourseCode { get; set; }
        public int? Semester { get; set; }


        public int? Year { get; set; }
        public int? UserID { get; set; }
        public string? Day { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int? Capacity { get; set; }
        public string? Room { get; set; }
        // ... الحقول الأخرى ...
        public int? InstructorID { get; set; }
        [ForeignKey("InstructorID")]
        public Instructor Instructor { get; set; }

        public SystemUser User { get; set; } // علاقة الربط

        [ForeignKey(nameof(CourseID))]
        public Course Course { get; set; }

        public ICollection<CourseRegistration> CourseRegistrations { get; set; }
            = new List<CourseRegistration>();
    }
}



