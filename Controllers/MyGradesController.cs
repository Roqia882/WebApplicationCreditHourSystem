using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication_Credit_hour_system.Models;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace WebApplication_Credit_hour_system.Controllers
{
    [Authorize] // 🔐 محمي بالـ JWT لضمان أمان بيانات الطالب
    [ApiController]
    [Route("api/[controller]")] // api/MyGrades
    public class MyGradesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MyGradesController(AppDbContext context)
        {
            _context = context;
        }

        // دالة مساعدة لفك واستخراج ID الطالب من التوكن الحالية
        private int GetCurrentStudentId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int id) ? id : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyGrades([FromQuery] string language = "en")
        {
            // 1️⃣ جلب ID الطالب من التوكن
            int studentId = GetCurrentStudentId();
            if (studentId == 0)
                return Unauthorized(new { success = false, message = language == "ar" ? "توكن غير صالح" : "Invalid Token" });

            // 2️⃣ جلب بيانات الطالب الأساسية
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
            if (student == null)
                return NotFound(new { success = false, message = language == "ar" ? "الطالب غير موجود" : "Student not found" });

            // 3️⃣ جلب كافة الدرجات المنشورة فقط لهذا الطالب
            var dbGrades = await (
                from g in _context.Grades
                join o in _context.CourseOfferings on g.OfferingID equals o.OfferingID
                join c in _context.Courses on o.CourseID equals c.CourseID
                where g.StudentID == studentId && g.IsPublished == true // 🔥 شرط الأمان: المنشور فقط
                select new
                {
                    g.GradeID,
                    c.CourseID,
                    CourseName = language == "ar" ? c.CourseNameAr : c.CourseNameEn,
                    g.NumericGrade,
                    g.LetterGrade,
                    g.GradePoints,
                    g.Percentage,
                    g.AcademicYear,
                    g.Semester,
                    g.Status,
                    CreditHours = c.CreditHours ?? 0,
                    Category = c.CourseCategory ?? "ENG"
                }
            ).ToListAsync();

            // 4️⃣ جلب الأرشيف والملخصات الفصلية المسجلة للطالب (من جدول الـ StudentSemesterResults)
            var semesterSummaries = await _context.StudentSemesterResults
                .Where(ssr => ssr.StudentID == studentId)
                .ToListAsync();

            // 5️⃣ تجميع وتصنيف المواد تبعا للترم والسنة وترتيبها تنازلياً (من السنة الحالية نزولاً لأولى)
            var academicHistory = dbGrades
                .GroupBy(g => new { g.AcademicYear, g.Semester })
                .OrderByDescending(group => group.Key.AcademicYear) // 🌟 ترتيب تنازلي حسب السنة الدراسية
                .ThenByDescending(group => group.Key.Semester)    // 🌟 ثم تنازلي حسب الترم
                .Select(group =>
                {
                    // البحث عن ملخص الترم المطابق من جدول الطلاب لمعرفة الـ GPA والـ CGPA التراكمي في تلك اللحظة
                    var summary = semesterSummaries
                        .FirstOrDefault(s => s.AcademicYear.ToString() == group.Key.AcademicYear.ToString()
                                          && s.Semester.ToString() == group.Key.Semester.ToString());

                    return new
                    {
                        // سطر المعلومات الأساسية للترم
                        academicYear = group.Key.AcademicYear,
                        semester = group.Key.Semester,
                        termLabel = language == "ar"
                            ? $"ترم: {group.Key.Semester} - سنة دراسية: {group.Key.AcademicYear}"
                            : $"Semester: {group.Key.Semester} - Academic Year: {group.Key.AcademicYear}",

                        // 📊 الحسابات والأرقام الخاصة بالترم الحالي والتراكمي كما طلبتِ (من الصورة 2 و3)
                        semesterHours = summary?.SemesterHours ?? group.Sum(x => x.CreditHours),
                        cumulativeHours = summary?.CumulativeHours ?? 0,
                        // 🌟 تحديث المسميات لرموز واضحة كما طلبتِ بالظبط
                        GPA = summary?.SemesterGPA ?? 0m,
                        CGPA = summary?.CumulativeGPA ?? 0m,
                        GPAU = summary?.SemesterGPAU ?? 0m,
                        CGPAU = summary?.CumulativeGPAU ?? 0m,
                        // 📝 تفاصيل درجات مادة مادة في هذا الترم
                        courses = group.Select(c => new
                        {
                            courseID = c.CourseID,
                            courseName = c.CourseName,
                            numericGrade = c.NumericGrade,
                            letterGrade = c.LetterGrade,
                            gradePoints = c.GradePoints,
                            percentage = c.Percentage,
                           // status = c.Status
                        }).ToList()
                    };
                }).ToList();

            // 6️⃣ الرد النهائي المتكامل والمرتب كما طلبتِ بالظبط ليعرض للفرونت إند
            return Ok(new
            {
                success = true,
                message = language == "ar" ? "تم جلب السجل الأكاديمي والدرجات بنجاح" : "Academic record and grades fetched successfully",
                data = new
                {
                    // الحقول العلوية: تفاصيل الطالب الأساسية والتقديرات الكلية الحالية
                    studentID = student.StudentID,
                    studentName = student.FullName ?? "غير معروف",
                    studentLevel = student.Year ?? "N/A", // المستوى الحالي للطالب
                                                          // 🌟 التصحيح المبرهن من الداتابيز الحين:
                    GPA = student.GPA ?? 0,    // معدل آخر ترم (اللي بيعتمد عليه التسجيل والـ Max Hours)
                    CGPA = student.CGPA ?? 0, // المعدل التراكمي الكلي المسجل حالياً
                    totalGradePoints = student.TotalGradePoints ?? 0,
                    totalPercentage = student.TotalPercentage ?? 0,
                    overallLetter = student.OverallLetter ?? (language == "ar" ? "غير محدد" : "N/A"),

                    // التاريخ الأكاديمي التفصيلي من الأحدث للأقدم
                    academicHistory = academicHistory
                }
            });
        }
    }
}