using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebApplication_Credit_hour_system.Models;

namespace WebApplication_Credit_hour_system.Controllers
{
    // ... (الـ usings والمشتقات)

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentController(AppDbContext context)
        {
            _context = context;
        }

        // دالة موحدة لجلب الـ ID
        private int GetCurrentStudentId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out int id) ? id : 0;
        }

        [HttpGet("profile")]
        public IActionResult GetProfile([FromQuery] string lang = "en")
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return Unauthorized();

            var student = _context.Students.FirstOrDefault(s => s.StudentID == studentId);
            if (student == null)
                return NotFound(new { success = false, message = lang == "ar" ? "الطالب غير موجود" : "Student not found" });

            // بنجيب رابط السيرفر (مثلاً https://localhost:7045) عشان الصورة تفتح مباشرة
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var profile = new StudentProfile
            {
                StudentID = student.StudentID,
                FullName = student.FullName,
                Name = lang == "ar" ? student.NameAr : student.NameEn,
                Email = student.Email,
                Major = lang == "ar" ? student.MajorAr : student.MajorEn,
                Year = student.Year,
                Semester = student.Semester,
                Phone = student.Phone,
                GPA = student.GPA ?? 0m,
                CompletedCreditHours = student.CompletedCreditHours ?? 0,

                // هنا بنضيف رابط الصورة، ولو مفيش صورة بنحط صورة افتراضية
                ProfilePictureUrl = !string.IsNullOrEmpty(student.ProfileImagePath)
                    ? baseUrl + student.ProfileImagePath
                    : baseUrl + "/uploads/profiles/default-avatar.png"
            };

            return Ok(new { success = true, data = profile });
        }

        [HttpPut("edit-student-profile")]
        public async Task<IActionResult> EditProfile([FromForm] EditProfileRequest request)
        {
            // 1. تحديد الطالب (زي ما عملنا في يوزرز كنترولر بالتوكن)
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int studentId = int.Parse(userIdClaim);

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
            if (student == null) return NotFound("الطالب غير موجود");

            // 2. تحديث البيانات النصية
            student.FullName = request.FullName ?? student.FullName;
            student.NameEn = request.NameEn ?? student.NameEn;
            student.NameAr = request.NameAr ?? student.NameAr;
            student.Email = request.Email ?? student.Email;
            student.Year = request.Year ?? student.Year;
            student.Semester = request.Semester ?? student.Semester;
            student.Phone = request.Phone ?? student.Phone;
            // دعم اللغتين للتخصص
            student.MajorEn = request.MajorEn ?? student.MajorEn;
            student.MajorAr = request.MajorAr ?? student.MajorAr;


            if (request.ProfilePicture != null)
            {
                // مسار الفولدر اللي هنخزن فيه الصور داخل المشروع
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                // اسم فريد للصورة عشان لو طالبين رفعوا صورتين بنفس الاسم
                var fileName = $"{Guid.NewGuid()}_{request.ProfilePicture.FileName}";
                var filePath = Path.Combine(folderPath, fileName);

                // حفظ الصورة في الفولدر
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.ProfilePicture.CopyToAsync(stream);
                }

                // حفظ "لينك" الصورة في قاعدة البيانات عشان نعرف نعرضها بعدين
                student.ProfileImagePath = $"/uploads/profiles/{fileName}";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تحديث البروفايل بنجاح", imagePath = student.ProfileImagePath });
        }
       
        [HttpPost("changepassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, [FromQuery] string lang = "en")
        {
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int studentId = int.Parse(userIdClaim);

            // 2. البحث عن الطالب في قاعدة البيانات
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
            {
                string notFoundMsg = lang == "ar" ? "الطالب غير موجود" : "Student not found";
                return NotFound(new { success = false, message = notFoundMsg });
            }

            // 3. التحقق من كلمة المرور القديمة
            if (student.Password != request.OldPassword)
            {
                string wrongMsg = lang == "ar" ? "كلمة المرور القديمة غير صحيحة" : "Old password is incorrect";
                return BadRequest(new { success = false, message = wrongMsg });
            }

            // 4. تحديث كلمة المرور وحفظ التغييرات
            student.Password = request.NewPassword;
            await _context.SaveChangesAsync();

            string message = lang == "ar" ? "تم تغيير كلمة المرور بنجاح" : "Password changed successfully";
            return Ok(new { success = true, message });
        }
    }
    }
