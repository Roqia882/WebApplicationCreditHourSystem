using Microsoft.AspNetCore.Mvc;
using WebApplication_Credit_hour_system.Models;
using System.Linq;

namespace WebApplication_Credit_hour_system.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ===================== LOGIN =====================
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginUserRequest request)
        {
            var student = _context.Students.FirstOrDefault(s =>
                s.StudentID == request.StudentID &&
                s.UniversityEmail == request.UniversityEmail &&
                s.Password == request.Password
            );

            if (student == null)
            {
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = request.Language == "ar"
                        ? "بيانات تسجيل الدخول غير صحيحة"
                        : "Invalid login credentials",
                    Token = null
                });
            }

            string token = JWTService.GenerateToken(student, _configuration);

            return Ok(new LoginResponse
            {
                Success = true,
                Message = request.Language == "ar"
                    ? "تم تسجيل الدخول بنجاح"
                    : "Login successful",
                Token = token
            });
        }

        // ===================== REGISTER =====================
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterUserRequest request)
        {
            // منع تكرار الإيميل
            if (_context.Students.Any(s => s.Email == request.Email))
            {
                string errorMsg = request.Language == "ar"
                    ? "هذا البريد الإلكتروني مسجل بالفعل"
                    : "Email already exists";

                return BadRequest(new { success = false, message = errorMsg });
            }

            // منع تكرار الـ StudentID
            if (_context.Students.Any(s => s.StudentID == request.StudentID))
            {
                string errorMsg = request.Language == "ar"
                    ? "رقم الطالب مسجل بالفعل"
                    : "StudentID already registered for another student";

                return BadRequest(new { success = false, message = errorMsg });
            }

            var student = new Student
            {
                StudentID = request.StudentID,
                NameEn = request.Name,
                NameAr = request.Name,
                Email = request.Email,
                Password = request.Password,
                Year = request.Year.ToString(),

                // باقي الحقول فارغة مبدئيًا
                Phone = "",
                MajorEn = "",
                MajorAr = ""
            };

            _context.Students.Add(student);
            _context.SaveChanges();

            // إنشاء توكن JWT
            string token = JWTService.GenerateToken(student, _configuration);

            string successMsg = request.Language == "ar"
                ? "تم التسجيل بنجاح"
                : "Register successful";

            return Ok(new
            {
                success = true,
                message = successMsg,
                token,
                data = new
                {
                    studentID = student.StudentID,
                    name = student.NameEn,
                    email = student.Email,
                    year = student.Year,
                    phone = student.Phone,
                    majorEn = student.MajorEn,
                    majorAr = student.MajorAr
                }
            });
        }

    }
}









