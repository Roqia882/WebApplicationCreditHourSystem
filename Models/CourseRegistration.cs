using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication_Credit_hour_system.Models;


namespace WebApplication_Credit_hour_system.Models
{
    
        [Table("CourseRegistration")]
        public class CourseRegistration
        {

        [Key]
        public int RegistrationID { get; set; }

        public int StudentID { get; set; }
        public int OfferingID { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string Status { get; set; }

        [ForeignKey(nameof(StudentID))]
        public Student Student { get; set; }

        [ForeignKey(nameof(OfferingID))]
        public CourseOffering CourseOffering { get; set; }
    }
}
