using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication_Credit_hour_system.Models;


namespace WebApplication_Credit_hour_system.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CoursesRegistrationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const int MaxTermHours = 14;

        public CoursesRegistrationController(AppDbContext context)
        {
            _context = context;
        }

        // 🌟 الدالة المعدلة بالكامل لحل مشكلة التكرار والبريكويست الفاضي 🌟
        private async Task<List<object>> GetCourseDetails(string courseId, string lang)
        {
            var responseList = new List<object>();

            // 1. جلب المتطلب السابق الفعلي من جدول البريكويست الصحيح
            var prereqRecord = await _context.CoursePrerequisites.FirstOrDefaultAsync(p => p.CourseID == courseId);
            string prereqName = prereqRecord != null
                ? prereqRecord.PrerequisiteCourseID
                : (lang == "ar" ? "لا يوجد" : "None");

            // 2. جلب العروض من الـ CourseOfferings للمادة
            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Include(o => o.Instructor)
                .Where(o => o.CourseID == courseId)
                .ToListAsync();

            if (offerings.Any())
            {
                // 🔥 الحل: بناخد العرض الأول فقط (المحاضرة الرئيسية) عشان نمنع تكرار السكاشن والمعامل في الـ Swagger
                var mainOffering = offerings.First();

                int currentCount = await _context.CourseRegistrations.CountAsync(r => r.OfferingID == mainOffering.OfferingID);

                responseList.Add(new
                {
                    courseID = mainOffering.CourseID,
                    courseName = lang == "ar" ? mainOffering.Course.CourseNameAr : mainOffering.Course.CourseNameEn,
                    instructor = lang == "ar" ? (mainOffering.Instructor?.NameAr ?? mainOffering.Instructor?.NameEn) : mainOffering.Instructor?.NameEn,
                    creditHours = mainOffering.Course.CreditHours,
                    schedule = $"{mainOffering.Day} {mainOffering.StartTime?.ToString(@"hh\:mm")} - {mainOffering.EndTime?.ToString(@"hh\:mm")}",
                    prerequisite = prereqName, // 🌟 هيعرض الكود الحقيقي للمادة المتطلبة (مثل ENG101)
                    enrollmentStatus = mainOffering.Capacity.HasValue ? $"{currentCount}/{mainOffering.Capacity}" : $"{currentCount}"
                });

                return responseList;
            }

            var courseOnly = await _context.Courses.FirstOrDefaultAsync(c => c.CourseID == courseId);
            if (courseOnly != null)
            {
                responseList.Add(new
                {
                    courseID = courseOnly.CourseID,
                    courseName = lang == "ar" ? courseOnly.CourseNameAr : courseOnly.CourseNameEn,
                    instructor = lang == "ar" ? "غير مدرج" : "Not Assigned",
                    creditHours = courseOnly.CreditHours,
                    schedule = lang == "ar" ? "غير محدد" : "Not Scheduled",
                    prerequisite = prereqName,
                    enrollmentStatus = lang == "ar" ? "مغلق" : "Closed"
                });
                return responseList;
            }

            return null;
        }

        [HttpPost("register/{courseId}")]
        public async Task<IActionResult> RegisterCourse(string courseId, [FromQuery] string lang = "en")
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return Unauthorized();
            /*var systemControl = await _context.SystemControls.FirstOrDefaultAsync(); //

             if (systemControl == null || !systemControl.IsRegistrationOpen) //
             {
                  return BadRequest(new { 
                      success = false, 
                      message = lang == "ar" ? "عذراً، باب التسجيل مغلق حالياً." : "Sorry, registration is currently closed." 
                  });
            }*/
            var systemControl = await _context.SystemControls.FirstOrDefaultAsync();

            // لو الجدول فاضي في الداتابيز (null) أو القيمة الخانة false، بنرفض التسجيل فوراً
            if (systemControl == null || !systemControl.IsRegistrationOpen)
            {
                return BadRequest(new
                {
                    success = false,
                    message = lang == "ar" ? "عذراً، باب التسجيل مغلق حالياً من قبل الإدارة." : "Sorry, registration is currently closed by the administration."
                });
            }

            var courseInfo = await GetCourseDetails(courseId, lang);

            var offerings = await _context.CourseOfferings
                .Include(o => o.Course)
                .Where(o => o.CourseID == courseId)
                .ToListAsync();

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (!offerings.Any() || student == null)
                return NotFound(new { success = false, message = lang == "ar" ? "المادة غير متوفرة" : "Course not available", course = courseInfo });

            CourseOffering selectedOffering = null;
            foreach (var off in offerings)
            {
                int currentOffCount = await _context.CourseRegistrations.CountAsync(r => r.OfferingID == off.OfferingID);
                if (!off.Capacity.HasValue || currentOffCount < off.Capacity.Value)
                {
                    selectedOffering = off;
                    break;
                }
            }
            if (selectedOffering == null) selectedOffering = offerings.First();

            if (await _context.CourseRegistrations.AnyAsync(r => r.StudentID == studentId && r.CourseOffering.CourseID == courseId))
                return BadRequest(new { success = false, message = lang == "ar" ? "أنت مسجل بالفعل في هذه المادة" : "Already registered in this course", course = courseInfo });
            // 4. لوجيك الـ GPA والمستوى
            if ((student.GPA ?? 0) < 1.0m && student.Semester != "1")
            {
                int.TryParse(selectedOffering.Course.Level?.ToString(), out int courseLevel);
                int studentYear = MapYearToNumber(student.Year);
                if (courseLevel > studentYear)
                    return BadRequest(new { success = false, message = lang == "ar" ? "يجب النجاح في المستوى الحالي أولاً" : "Must pass current level first", course = courseInfo });
            }
            // 5. تحقق الطالب المستجد والمتطلبات
            bool isNewStudent = (MapYearToNumber(student.Year) == 1 && student.Semester == "1");
            if (isNewStudent)
            {
                if (selectedOffering.Course.Level?.ToString() != "1" || selectedOffering.Course.Semester?.ToString() != "1")
                    return BadRequest(new { success = false, message = lang == "ar" ? "كطالب مستجد، مسموح لك فقط بمواد المستوى الأول" : "New students only allowed Level 1 courses", course = courseInfo });
            }
            else
            {
                var prerequisite = await _context.CoursePrerequisites.FirstOrDefaultAsync(p => p.CourseID == courseId);
                if (prerequisite != null)
                {
                    bool hasPassedPre = await _context.Grades.AnyAsync(g => g.StudentID == studentId &&
                                                                       g.CourseOffering.CourseID == prerequisite.PrerequisiteCourseID &&
                                                                       (double)(g.GradePoints ?? 0) >= 1.0);
                    if (!hasPassedPre)
                        return BadRequest(new { success = false, message = lang == "ar" ? $"يجب النجاح في {prerequisite.PrerequisiteCourseID} أولاً" : $"Pass {prerequisite.PrerequisiteCourseID} first", course = courseInfo });
                }
            }
            // 6.
            // 🌟 التعديل السليم لحساب الساعات مع عمل Include للـ Course و الـ CourseOffering لتجنب الـ Null 🌟
            //تحقق الساعات
            int studentMaxHours = student.MaxAllowedHours ?? MaxTermHours;

           
            var currentHours = await _context.CourseRegistrations
                .Include(r => r.CourseOffering)
                    .ThenInclude(co => co.Course)
                .Where(r => r.StudentID == studentId
                         && r.CourseOffering.Course.AcademicYear == selectedOffering.Course.AcademicYear
                         && r.CourseOffering.Semester == selectedOffering.Semester)
                .SumAsync(r => r.CourseOffering.Course.CreditHours) ?? 0;

            if (currentHours + (selectedOffering.Course.CreditHours ?? 0) > studentMaxHours)
                return BadRequest(new { success = false, message = lang == "ar" ? $"تجاوزت الحد الأقصى للساعات ({studentMaxHours})" : "Exceeded hours limit", course = courseInfo });
            // 7. تحديد الحالة (اللوجيك الأصلي بتاعك)
            int currentCount = await _context.CourseRegistrations.CountAsync(r => r.OfferingID == selectedOffering.OfferingID);
            string status = (currentCount + 1 >= 10) ? "Registered" : "Waiting";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var registration = new CourseRegistration
                {
                    StudentID = studentId,
                    OfferingID = selectedOffering.OfferingID,
                    RegistrationDate = DateTime.Now,
                    Status = status
                };
                _context.CourseRegistrations.Add(registration);

                // 🌟 السحر بتاعنا: إنشاء سطر فاضي في جدول الـ Grades فوراً مع التسجيل
                var emptyGradeRecord = new WebApplication_Credit_hour_system.Models.Grade
                {
                    StudentID = studentId,
                    OfferingID = selectedOffering.OfferingID,
                    AcademicYear = selectedOffering.Course?.AcademicYear ?? DateTime.Now.Year,
                    Semester = selectedOffering.Semester ?? 1,
                    IsPublished = false,
                    Status = "Draft", // لسه مسودة لحد ما الدكتور يرصد

                    // كل خانات الدرجات التفصيلية بتنزل NULL وفاضية مستنية الدكتور
                    AttendanceGrade = null,
                    AssignmentsGrade = null,
                    QuizzesGrade = null,
                    MidtermExamGrade = null,
                    PracticalGrade = null,
                    OralGrade = null,
                    FinalExamGrade = null,
                    NumericGrade = null,
                    Percentage = null,
                    LetterGrade = null,
                    GradePoints = null
                };
                _context.Grades.Add(emptyGradeRecord);
                /*
                var lectureSchedule = new ScheduleLecture
                {
                    StudentID = studentId,
                    CourseID = courseId,
                    Semester = selectedOffering.Semester?.ToString(),
                    Year = MapYearToNumber(student.Year),
                    Day = selectedOffering.Day,
                    StartTime = selectedOffering.StartTime,
                    EndTime = selectedOffering.EndTime,
                    Room = selectedOffering.Room,
                    SessionType = "lecture",
                    InstructorID = selectedOffering.InstructorID
                };
                _context.Schedule.Add(lectureSchedule);

                // 🌟 التعديل السليم للتأكد من جلب السكاشن والمعامل سواء كانت القيمة null أو 0 وتجنب حالة الأحرف 🌟
                var cleanCourseId = courseId.Trim().ToLower();
                var otherSessions = await _context.Schedule

                    .Where(s => s.CourseID == courseId
                             && s.SessionType.ToLower() != "lecture"
                            && (EF.Property<int?>(s, "StudentID") == null || s.StudentID == 0))
                    .ToListAsync();

                foreach (var session in otherSessions)
                {
                    var studentSession = new ScheduleLecture
                    {
                        StudentID = studentId,
                        CourseID = courseId,
                        Semester = selectedOffering.Semester?.ToString(),
                        Year = MapYearToNumber(student.Year),
                        Day = session.Day,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime,
                        Room = session.Room,
                        SessionType = session.SessionType,
                        InstructorID = session.InstructorID
                    };
                    _context.Schedule.Add(studentSession);
                }
                */
                // تحديث قائمة الانتظار لو المادة اتملت (اللوجيك القديم)
                if (status == "Registered")
                {
                    var waitingList = await _context.CourseRegistrations
                        .Where(r => r.OfferingID == selectedOffering.OfferingID && r.Status == "Waiting")
                        .ToListAsync();
                    foreach (var reg in waitingList) { reg.Status = "Registered"; }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                // تحديث معلومات الكورس بعد النجاح
                var updatedCourseInfo = await GetCourseDetails(courseId, lang);
                return Ok(new { success = true, status = status, course = updatedCourseInfo });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message, course = courseInfo });
            }
        }

        [HttpDelete("drop/{courseId}")]
        public async Task<IActionResult> DropCourse(string courseId, [FromQuery] string lang = "en")
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return Unauthorized();

            var courseInfo = await GetCourseDetails(courseId, lang);

            var registration = await _context.CourseRegistrations
                .Include(r => r.CourseOffering)
                .FirstOrDefaultAsync(r => r.StudentID == studentId && r.CourseOffering.CourseID == courseId);

            if (registration == null)
                return BadRequest(new { success = false, message = lang == "ar" ? "أنت غير مسجل" : "Not registered", course = courseInfo });

            /* var scheduleItems = _context.Schedule.Where(s => s.StudentID == studentId && s.CourseID == courseId);
             if (scheduleItems.Any()) _context.Schedule.RemoveRange(scheduleItems);
            */
            // 🌟 نجيب سجل الدرجات المرتبط بالتسجيل ده عشان نمسحه
            var gradeRecord = await _context.Grades
                .FirstOrDefaultAsync(g => g.StudentID == studentId && g.OfferingID == registration.OfferingID);

            if (gradeRecord != null)
            {
                _context.Grades.Remove(gradeRecord);
            }

            _context.CourseRegistrations.Remove(registration);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = lang == "ar" ? "تم حذف المادة " : "Course  dropped", course = courseInfo });
        }

        private int GetCurrentStudentId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int id) ? id : 0;
        }

        private int MapYearToNumber(string year)
        {
            if (string.IsNullOrEmpty(year)) return 0;
            return year.ToLower().Trim() switch
            {
                "first" => 1,
                "second" => 2,
                "third" => 3,
                "fourth" => 4,
                "fifth" => 5,
                _ => int.TryParse(year, out int n) ? n : 0
            };
        }
    }
}