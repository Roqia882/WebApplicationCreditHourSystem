using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using WebApplication_Credit_hour_system.Models;

namespace WebApplication_Credit_hour_system.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🔐 كل endpoints هنا محتاجين توكن صالح
    public class CoursesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CoursesController(AppDbContext context)
        {
            _context = context;
        }

        // ===================== GET ALL COURSES =====================
        [HttpGet("all")]
        public IActionResult GetAllCourses([FromQuery] string lang = "en")
        {
            // 1. جلب البيانات الأساسية (تعديل: سحب الاسمين معاً)
            var coursesData = _context.Courses
                .Select(c => new
                {
                    c.CourseID,
                    c.CourseNameAr, // 👈 إضافة الاسم العربي
                    c.CourseNameEn, // 👈 إضافة الاسم الإنجليزي
                    c.CreditHours,
                    Level = c.Level,
                    Semester = c.Semester,
                })
                .ToList();

            // 2. التجميع والترتيب كما هو
            var groupedCourses = coursesData
                .GroupBy(c => c.Level)
                .OrderBy(g => g.Key)
                .Select(levelGroup => new
                {
                    Level = levelGroup.Key,
                    Semesters = levelGroup
                        .GroupBy(c => c.Semester)
                        .OrderBy(s => s.Key)
                        .Select(semGroup => new
                        {
                            Semester = semGroup.Key,
                            Courses = semGroup.Select(c => new
                            {
                                c.CourseID,
                                c.CourseNameAr, // 👈 يظهر في الرد النهائي
                                c.CourseNameEn, // 👈 يظهر في الرد النهائي
                                c.CreditHours,
                            }).ToList()
                        }).ToList()
                }).ToList();

            string message = lang.ToLower() == "ar"
                ? "تم جلب المواد الدراسية بنجاح"
                : "Courses fetched successfully";

            return Ok(new
            {
                success = true,
                message,
                data = groupedCourses
            });
        }
    }
}