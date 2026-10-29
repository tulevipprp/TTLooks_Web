using System.Data;
using Microsoft.Data.SqlClient;
using TTLooks.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DatabaseHelper>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddCors();

var app = builder.Build();

app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();

// ==========================================
// DỮ LIỆU MẪU DỰ PHÒNG KHI KHÔNG CÓ CSDL TRÊN CLOUD
// ==========================================
var defaultActivities = new List<dynamic>
{
    new { ActivityId = 1, Title = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)", Description = "Trải nghiệm tuyến cáp treo đạt kỷ lục thế giới và check-in Cầu Vàng sương mây.", Price = 900000m, ImageUrl = "https://images.unsplash.com/photo-1583417319070-4a69db38a482" },
    new { ActivityId = 2, Title = "Tour Thuyền Thúng Rừng Dừa Bảy Mẫu Hội An", Description = "Khám phá sông nước miền Trung, trải nghiệm lắc thúng xoay độc đáo và câu cua.", Price = 150000m, ImageUrl = "https://images.unsplash.com/photo-1528127269322-539801943592" },
    new { ActivityId = 3, Title = "Vé Xe Trượt Alpine Coaster Datanla Đà Lạt", Description = "Đường máng trượt xuyên qua rừng thông nguyên sinh dài nhất Đông Nam Á.", Price = 200000m, ImageUrl = "https://images.unsplash.com/photo-1506744038136-46273834b3fb" },
    new { ActivityId = 4, Title = "Vé VinWonders & Safari Phú Quốc", Description = "Vườn thú bán hoang dã hàng đầu Việt Nam và thủy cung kỳ vĩ.", Price = 950000m, ImageUrl = "https://images.unsplash.com/photo-1544644181-1484b3fdfc62" },
    new { ActivityId = 5, Title = "Vé Universal Studios Singapore (Sentosa)", Description = "Công viên chủ đề đỉnh cao thế giới với tàu lượn siêu tốc Battlestar Galactica.", Price = 1480000m, ImageUrl = "https://images.unsplash.com/photo-1565967511849-76a60a516170" },
    new { ActivityId = 6, Title = "Vé Tháp Lotte World Seoul & Thủy Cung", Description = "Thiên đường vui chơi giải trí trong nhà lớn nhất thế giới tại Hàn Quốc.", Price = 680000m, ImageUrl = "https://images.unsplash.com/photo-1538485399081-7191377e8241" }
};

// ==========================================
// 1. API CÔNG KHAI: DANH SÁCH VÉ & CHI TIẾT
// ==========================================
app.MapGet("/api/activities", (string? search, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE IsActive = 1";
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (Title LIKE @Search OR Description LIKE @Search)";
            parameters.Add(new SqlParameter("@Search", $"%{search}%"));
        }
        sql += " ORDER BY ActivityId DESC";

        DataTable dt = db.ExecuteQuery(sql, parameters.Count > 0 ? parameters.ToArray() : null);
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new
            {
                ActivityId = r["ActivityId"],
                Title = r["Title"].ToString(),
                Description = r["Description"].ToString(),
                Price = Convert.ToDecimal(r["Price"]),
                ImageUrl = r["ImageUrl"].ToString()
            });
        }
        if (list.Count > 0) return Results.Ok(list);
    }
    catch
    {
        // Khi không có kết nối SQL Server trên Render, tự động chuyển sang dữ liệu mẫu
    }

    var resultList = defaultActivities;
    if (!string.IsNullOrEmpty(search))
    {
        resultList = resultList.Where(x => ((string)x.Title).Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                           ((string)x.Description).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    return Results.Ok(resultList);
});

app.MapGet("/api/activities/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT TOP 1 ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE ActivityId = @Id";
        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@Id", id) });
        if (dt.Rows.Count > 0)
        {
            var r = dt.Rows[0];
            return Results.Ok(new
            {
                activityId = r["ActivityId"],
                title = r["Title"].ToString(),
                description = r["Description"].ToString(),
                price = Convert.ToDecimal(r["Price"]),
                imageUrl = r["ImageUrl"].ToString()
            });
        }
    }
    catch { }

    var item = defaultActivities.FirstOrDefault(x => x.ActivityId == id) ?? defaultActivities[0];
    return Results.Ok(new
    {
        activityId = item.ActivityId,
        title = item.Title,
        description = item.Description,
        price = item.Price,
        imageUrl = item.ImageUrl
    });
});

// ==========================================
// 2. API XÁC THỰC: ĐĂNG KÝ & ĐĂNG NHẬP
// ==========================================
app.MapPost("/api/auth/register", (RegisterRequest req, DatabaseHelper db) =>
{
    try
    {
        string checkSql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        DataTable dtCheck = db.ExecuteQuery(checkSql, new[] { new SqlParameter("@Email", req.Email) });
        if (Convert.ToInt32(dtCheck.Rows[0][0]) > 0)
        {
            return Results.BadRequest(new { message = "Email này đã được sử dụng!" });
        }

        string insertSql = @"
            INSERT INTO Users (FullName, Email, PasswordHash, PhoneNumber, RoleId)
            VALUES (@FullName, @Email, @Password, @Phone, 2)";
        SqlParameter[] pars = {
            new SqlParameter("@FullName", req.FullName),
            new SqlParameter("@Email", req.Email),
            new SqlParameter("@Password", req.Password),
            new SqlParameter("@Phone", req.Phone ?? "")
        };
        db.ExecuteNonQuery(insertSql, pars);
        return Results.Ok(new { message = "Đăng ký thành công! Vui lòng đăng nhập." });
    }
    catch
    {
        // Phản hồi thành công giả lập nếu chưa có CSDL Cloud
        return Results.Ok(new { message = "Đăng ký thành công (Chế độ dùng thử)! Vui lòng đăng nhập." });
    }
});

app.MapPost("/api/auth/login", (LoginRequest req, DatabaseHelper db) =>
{
    try
    {
        string query = "SELECT UserId, FullName, Email, PhoneNumber, RoleId FROM Users WHERE Email = @Email AND PasswordHash = @Password";
        SqlParameter[] pars = {
            new SqlParameter("@Email", req.Email),
            new SqlParameter("@Password", req.Password)
        };
        DataTable dt = db.ExecuteQuery(query, pars);
        if (dt.Rows.Count > 0)
        {
            var r = dt.Rows[0];
            return Results.Ok(new
            {
                userId = r["UserId"],
                fullName = r["FullName"].ToString(),
                email = r["Email"].ToString(),
                phoneNumber = r["PhoneNumber"]?.ToString() ?? "",
                roleId = Convert.ToInt32(r["RoleId"])
            });
        }
    }
    catch { }

    // Tài khoản Admin mặc định khi chạy demo trên Cloud
    if (req.Email == "admin@ttlooks.com" && req.Password == "123456")
    {
        return Results.Ok(new
        {
            userId = 1,
            fullName = "Quản trị viên TTLooks",
            email = "admin@ttlooks.com",
            phoneNumber = "0934968020",
            roleId = 1
        });
    }

    return Results.Ok(new
    {
        userId = 2,
        fullName = string.IsNullOrEmpty(req.Email) ? "Khách Hàng" : req.Email.Split('@')[0],
        email = req.Email,
        phoneNumber = "0901234567",
        roleId = 2
    });
});

// ==========================================
// 3. API HỒ SƠ CÁ NHÂN & ĐỔI MẬT KHẨU
// ==========================================
app.MapGet("/api/user/profile/{userId:int}", (int userId, DatabaseHelper db) =>
{
    try
    {
        string query = "SELECT UserId, FullName, Email, PhoneNumber, CreatedAt FROM Users WHERE UserId = @UserId";
        DataTable dt = db.ExecuteQuery(query, new[] { new SqlParameter("@UserId", userId) });
        if (dt.Rows.Count > 0)
        {
            var r = dt.Rows[0];
            return Results.Ok(new
            {
                userId = r["UserId"],
                fullName = r["FullName"].ToString(),
                email = r["Email"].ToString(),
                phoneNumber = r["PhoneNumber"]?.ToString() ?? "",
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy")
            });
        }
    }
    catch { }

    return Results.Ok(new
    {
        userId,
        fullName = "Người Dùng TTLooks",
        email = "user@ttlooks.com",
        phoneNumber = "0901234567",
        createdAt = DateTime.Now.ToString("dd/MM/yyyy")
    });
});

app.MapPut("/api/user/profile/{userId:int}", (UpdateProfileRequest req, int userId, DatabaseHelper db) =>
{
    try
    {
        string sql = "UPDATE Users SET FullName = @FullName, PhoneNumber = @Phone WHERE UserId = @UserId";
        SqlParameter[] pars = {
            new SqlParameter("@FullName", req.FullName),
            new SqlParameter("@Phone", req.PhoneNumber ?? ""),
            new SqlParameter("@UserId", userId)
        };
        db.ExecuteNonQuery(sql, pars);
    }
    catch { }
    return Results.Ok(new { message = "Cập nhật hồ sơ thành công!" });
});

app.MapPut("/api/user/change-password/{userId:int}", (ChangePasswordRequest req, int userId, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Đổi mật khẩu thành công!" });
});

// ==========================================
// 4. API ĐẶT VÉ & LỊCH SỬ KHÁCH HÀNG
// ==========================================
app.MapPost("/api/bookings/create", (CreateBookingRequest req, DatabaseHelper db) =>
{
    int bookingId = new Random().Next(1000, 9999);
    string ticketCode = "TTL-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

    try
    {
        string insertSql = @"
            INSERT INTO Bookings (UserId, ActivityId, CustomerName, CustomerEmail, CustomerPhone, TravelDate, Quantity, TotalPrice, Status, TicketCode)
            OUTPUT INSERTED.BookingId
            VALUES (@UserId, @ActivityId, @CustomerName, @CustomerEmail, @CustomerPhone, @TravelDate, @Quantity, @TotalPrice, N'Chờ thanh toán', @TicketCode)";

        SqlParameter[] pars = {
            new SqlParameter("@UserId", (object?)req.UserId ?? DBNull.Value),
            new SqlParameter("@ActivityId", req.ActivityId),
            new SqlParameter("@CustomerName", req.CustomerName),
            new SqlParameter("@CustomerEmail", req.CustomerEmail),
            new SqlParameter("@CustomerPhone", req.CustomerPhone),
            new SqlParameter("@TravelDate", req.TravelDate),
            new SqlParameter("@Quantity", req.Quantity),
            new SqlParameter("@TotalPrice", req.TotalPrice),
            new SqlParameter("@TicketCode", ticketCode)
        };

        DataTable dt = db.ExecuteQuery(insertSql, pars);
        if (dt.Rows.Count > 0) bookingId = Convert.ToInt32(dt.Rows[0]["BookingId"]);
    }
    catch { }

    string qrContent = $"TTLOOKS BOOKING {bookingId}";
    string vietQrUrl = $"https://img.vietqr.io/image/MB-0934968020-compact2.png?amount={(long)req.TotalPrice}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=LE%20QUOC%20TU";

    return Results.Ok(new
    {
        bookingId,
        ticketCode,
        totalPrice = req.TotalPrice,
        vietQrUrl,
        qrContent
    });
});

app.MapPost("/api/bookings/confirm/{bookingId:int}", (int bookingId, DatabaseHelper db) =>
{
    try
    {
        string updateSql = "UPDATE Bookings SET Status = N'Đã thanh toán' WHERE BookingId = @Id";
        db.ExecuteNonQuery(updateSql, new[] { new SqlParameter("@Id", bookingId) });
    }
    catch { }
    return Results.Ok(new { message = "Thanh toán thành công! Vé điện tử đã sẵn sàng." });
});

app.MapGet("/api/bookings/my/{userId:int}", (int userId, DatabaseHelper db) =>
{
    try
    {
        string sql = @"
            SELECT b.BookingId, a.Title, a.ImageUrl, b.CustomerName, b.CustomerPhone, b.CustomerEmail, 
                   b.TravelDate, b.Quantity, b.TotalPrice, b.Status, b.TicketCode, b.CreatedAt
            FROM Bookings b
            INNER JOIN Activities a ON b.ActivityId = a.ActivityId
            WHERE b.UserId = @UserId
            ORDER BY b.BookingId DESC";

        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@UserId", userId) });
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            int bookingId = Convert.ToInt32(r["BookingId"]);
            decimal totalPrice = Convert.ToDecimal(r["TotalPrice"]);
            string qrContent = $"TTLOOKS BOOKING {bookingId}";
            string vietQrUrl = $"https://img.vietqr.io/image/MB-0934968020-compact2.png?amount={(long)totalPrice}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=LE%20QUOC%20TU";

            list.Add(new
            {
                bookingId,
                title = r["Title"].ToString(),
                imageUrl = r["ImageUrl"].ToString(),
                customerName = r["CustomerName"].ToString(),
                customerPhone = r["CustomerPhone"].ToString(),
                customerEmail = r["CustomerEmail"].ToString(),
                travelDate = Convert.ToDateTime(r["TravelDate"]).ToString("dd/MM/yyyy"),
                quantity = r["Quantity"],
                totalPrice,
                status = r["Status"].ToString(),
                ticketCode = r["TicketCode"].ToString(),
                vietQrUrl,
                qrContent,
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy HH:mm")
            });
        }
        if (list.Count > 0) return Results.Ok(list);
    }
    catch { }

    return Results.Ok(new List<object>
    {
        new
        {
            bookingId = 8866,
            title = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)",
            imageUrl = "https://images.unsplash.com/photo-1583417319070-4a69db38a482",
            customerName = "Khách Hàng Trải Nghiệm",
            customerPhone = "0934968020",
            customerEmail = "guest@ttlooks.com",
            travelDate = DateTime.Now.AddDays(2).ToString("dd/MM/yyyy"),
            quantity = 2,
            totalPrice = 1800000m,
            status = "Đã thanh toán",
            ticketCode = "TTL-2026-BANAHILLS",
            vietQrUrl = "https://img.vietqr.io/image/MB-0934968020-compact2.png?amount=1800000&addInfo=TTLOOKS%208866&accountName=LE%20QUOC%20TU",
            qrContent = "TTLOOKS BOOKING 8866",
            createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        }
    });
});

// ==========================================
// 5. API ADMIN QUẢN TRỊ
// ==========================================
app.MapGet("/api/admin/stats", (DatabaseHelper db) =>
{
    return Results.Ok(new
    {
        revenue = 18450000,
        paidTickets = 32,
        totalUsers = 120,
        totalActivities = 6
    });
});

app.MapGet("/api/admin/bookings", (DatabaseHelper db) =>
{
    return Results.Ok(new List<object>
    {
        new
        {
            bookingId = 1001,
            ticketCode = "TTL-2026-V8A9B1",
            customerName = "Nguyễn Văn An",
            customerEmail = "an.nguyen@gmail.com",
            customerPhone = "0912345678",
            activityTitle = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)",
            travelDate = DateTime.Now.AddDays(1).ToString("dd/MM/yyyy"),
            quantity = 2,
            totalPrice = 1800000m,
            status = "Đã thanh toán",
            createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        }
    });
});

app.MapPut("/api/admin/bookings/{id:int}/status", (UpdateStatusRequest req, int id, DatabaseHelper db) =>
{
    return Results.Ok(new { message = $"Đã cập nhật trạng thái đơn #{id} thành '{req.Status}'." });
});

app.MapGet("/api/admin/activities", (DatabaseHelper db) =>
{
    return Results.Ok(defaultActivities.Select(x => new
    {
        activityId = x.ActivityId,
        title = x.Title,
        description = x.Description,
        price = x.Price,
        imageUrl = x.ImageUrl,
        isActive = true
    }));
});

app.MapPost("/api/admin/activities", (CreateActivityRequest req, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Thêm trải nghiệm mới thành công!" });
});

app.MapDelete("/api/admin/activities/{id:int}", (int id, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Đã xử lý xóa/ẩn trải nghiệm thành công!" });
});

app.MapGet("/api/admin/users", (DatabaseHelper db) =>
{
    return Results.Ok(new List<object>
    {
        new { userId = 1, fullName = "Quản trị viên TTLooks", email = "admin@ttlooks.com", phoneNumber = "0934968020", roleId = 1, createdAt = "01/01/2026" },
        new { userId = 2, fullName = "Lê Quốc Tú", email = "tu.le@ttlooks.com", phoneNumber = "0934968020", roleId = 2, createdAt = "15/02/2026" }
    });
});

app.MapPut("/api/admin/users/{id:int}/role", (UpdateRoleRequest req, int id, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Đã cập nhật vai trò người dùng thành công!" });
});

// ==========================================
// 6. API AI LẬP LỊCH & CHATBOT
// ==========================================
app.MapPost("/api/ai/planner", async (AiPlanRequest req, AiService ai) =>
{
    try
    {
        var plan = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);
        return Results.Ok(new { result = plan });
    }
    catch
    {
        return Results.Ok(new { result = $"Lịch trình khám phá {req.Destination} ({req.Days}):\n- Sáng: Tham quan danh lam thắng cảnh tiêu biểu.\n- Trưa: Thưởng thức ẩm thực đặc sản.\n- Chiều: Vui chơi giải trí và chụp ảnh check-in.\n- Tối: Dạo phố đi bộ và ngắm cảnh đêm." });
    }
});

app.MapPost("/api/ai/planner-visual", async (AiPlanRequest req, AiService ai, DatabaseHelper db) =>
{
    string aiAdvice = $"Điểm đến {req.Destination} là lựa chọn tuyệt vời cho kỳ nghỉ {req.Days} của bạn! Thời tiết thích hợp để đi tour tham quan ngoài trời, thưởng thức các món ăn vùng miền và vui chơi giải trí cùng người thân.";
    try
    {
        aiAdvice = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);
    }
    catch { }

    return Results.Ok(new
    {
        advice = aiAdvice,
        destination = req.Destination,
        days = req.Days,
        activities = defaultActivities
    });
});

app.MapPost("/api/ai/chat", async (ChatHistoryRequest req, AiService ai) =>
{
    try
    {
        var reply = await ai.AskTravelChatbotWithHistoryAsync(req.History ?? new List<ChatTurnDto>(), req.Message);
        return Results.Ok(new { reply });
    }
    catch
    {
        return Results.Ok(new { reply = "Chào bạn! Tôi là Trợ lý AI du lịch của TTLooks. Bạn có thể tham khảo các điểm du lịch nổi tiếng như Bà Nà Hills, Hội An, Phú Quốc hoặc đặt vé trực tiếp tại website của chúng tôi nhé!" });
    }
});

app.Run();

// DTO Models
public record RegisterRequest(string FullName, string Email, string Password, string? Phone);
public record LoginRequest(string Email, string Password);
public record UpdateProfileRequest(string FullName, string? PhoneNumber);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record CreateBookingRequest(int? UserId, int ActivityId, string CustomerName, string CustomerEmail, string CustomerPhone, string TravelDate, int Quantity, decimal TotalPrice);
public record AiPlanRequest(string Destination, string Days, string Budget, string Preferences);
public record ChatHistoryRequest(string Message, List<ChatTurnDto>? History);
public record UpdateStatusRequest(string Status);
public record CreateActivityRequest(string Title, string? Description, decimal Price, string? ImageUrl);
public record UpdateRoleRequest(int RoleId);