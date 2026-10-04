using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication_Credit_hour_system.Models;

namespace WebApplication_Credit_hour_system.Controllers
{
     [ApiController]
        [Route("api/[controller]")]
        [Authorize] // 🔒 يحمي كل الـ endpoints في هذا الكلاس بالـ JWT
        public class DashboardController : ControllerBase
        {
            private readonly AppDbContext _context;
            private const int TotalCreditHours = 160; // مجموع الساعات المعتمدة

            public DashboardController(AppDbContext context)
            {
                _context = context;
            }

            // ========================= GET Student Dashboard =========================
            [HttpGet]
            public IActionResult GetDashboard()
            {
                // 🔑 نجيب StudentID من الـ token
                var studentIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (studentIdClaim == null)
                    return Unauthorized(new { success = false, message = "Invalid token" });

                int studentId = int.Parse(studentIdClaim.Value);

                // نجيب بيانات الطالب
                var student = _context.Students
                    .Where(s => s.StudentID == studentId)
                    .Select(s => new
                    {
                        s.StudentID,
                        NameEn = s.NameEn,
                        NameAr = s.NameAr,
                        s.GPA,
                        CompletedCreditHours = s.CompletedCreditHours
                    })
                    .FirstOrDefault();

                if (student == null)
                    return NotFound(new { success = false, message = "Student not found" });

                // نحسب Remaining Credit Hours
                int remainingCredits = TotalCreditHours - (student.CompletedCreditHours??0);

                // نحسب Overall Progress كنسبة مئوية
                double overallProgress = ((double)student.CompletedCreditHours / TotalCreditHours) * 100;

                // نرجع الرد جاهز للواجهة
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        student.StudentID,
                        NameEn = student.NameEn,
                        NameAr = student.NameAr,
                        student.GPA,
                        CompletedCreditHours = student.CompletedCreditHours,
                        RemainingCreditHours = remainingCredits,
                        OverallProgress = Math.Round(overallProgress, 2) // تقريبًا 2 رقم عشري
                    }
                });
            }
        }
    }



