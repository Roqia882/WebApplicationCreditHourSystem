using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WebApplication_Credit_hour_system.Models;

namespace WebApplication_Credit_hour_system.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // حماية الـ Controller بالكامل بالـ JWT
    public class ScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScheduleController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetMySchedule([FromQuery] string lang = "en")
        {
            // 1. جلب ID الطالب من الـ Token (سليم وقاري 1)
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return Unauthorized();

            // 2. جلب الـ OfferingIDs اللي الطالب مسجل فيها
            var registeredOfferingIds = await _context.CourseRegistrations
                .Where(cr => cr.StudentID == studentId && (cr.Status == "Registered" || cr.Status == "Active"))
                .Select(cr => cr.OfferingID)
                .ToListAsync();

            if (!registeredOfferingIds.Any())
            {
                return Ok(new { success = true, message = "الطالب مش مسجل في أي مواد حالياً", data = new List<object>() });
            }

            // 3. جلب الـ CourseIDs المقابلة ليها من جدول الـ Offerings
            var studentCourseIds = await _context.CourseOfferings
                .Where(co => registeredOfferingIds.Contains(co.OfferingID))
                .Select(co => co.CourseID)
                .ToListAsync();

            if (!studentCourseIds.Any())
            {
                return Ok(new { success = true, message = "لم يتم العثور على أكواد مواد لهذه الأطروحات", data = new List<object>() });
            }

            // تنظيف أكواد المواد تماماً وتحويلها لـ سمول وبدون مسافات
            var cleanStudentCourseIds = studentCourseIds
                .Where(id => id != null)
                .Select(id => id.Trim().ToLower())
                .ToList();

            // 4. جلب جدول المواعيد بالكامل في الـ Memory لعمل التنظيف الفعلي
            var allSchedules = await _context.Schedule
                .Include(s => s.Course)
                .Include(s => s.Instructor)
                .ToListAsync();

            // 5. الفلترة الذكية داخل الـ C# لتخطي عيوب مسافات الـ SQL Server والـ الكابيتال/سمول
            var filteredSchedule = allSchedules
                .Where(s => s.CourseID != null && cleanStudentCourseIds.Contains(s.CourseID.Trim().ToLower()))
                .OrderBy(s => s.Day)
                .ToList();

            // 6. التنسيق النهائي للـ Response
            var response = filteredSchedule.Select(s => new
            {
                s.ScheduleID,
                CourseID = s.CourseID?.Trim(),
                CourseName = lang == "ar" ? (s.Course?.CourseNameAr ?? s.CourseID?.Trim()) : (s.Course?.CourseNameEn ?? s.CourseID?.Trim()),
                InstructorName = lang == "ar" ? (s.Instructor?.NameAr ?? $"دكتور {s.InstructorID?.ToString()}") : (s.Instructor?.NameEn ?? $"Dr. {s.InstructorID?.ToString()}"),
                // تنظيف شكل اليوم (مثلاً mon تخليها Monday أو تسيبيها زي ما هي متسجلة)
                Day = s.Day?.Trim(),
                StartTime = s.StartTime.HasValue ? s.StartTime.Value.ToString(@"hh\:mm") : "00:00",
                EndTime = s.EndTime.HasValue ? s.EndTime.Value.ToString(@"hh\:mm") : "00:00",
                Room = s.Room ?? (lang == "ar" ? "غير محدد" : "Not Assigned"),
                SessionType = s.SessionType?.Trim()
            }).ToList();

            return Ok(new { success = true, data = response });
        }
        // ميثود مساعدة لجلب ID الطالب من التوكن (حل خطأ CS0103)
        private int GetCurrentStudentId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int id) ? id : 0;
        }
    }
}